using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.QueryProcessing;
using SqlBuildingBlocks.Utils;
using System.Collections.Concurrent;
using System.Data;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.Utils;

/// <summary>
/// Behavioral, cache-reuse, and concurrency tests for <see cref="CompiledQueryDispatch"/>,
/// the per-element-type compiled-delegate cache that replaced
/// <see cref="ReflectionHelper"/> in the QueryEngine hot path (#129, Wave 12 of
/// /uber-report 2026-05-09).
///
/// These tests exercise the cache through the QueryEngine public API rather than
/// poking at the cache directly, because that is the only contract callers actually
/// use. The internal cache-count hooks (<see cref="CompiledQueryDispatch.ApplyFilterCacheCount"/>
/// etc.) are used only to verify reuse — they are not part of the public surface.
/// </summary>
public class CompiledQueryDispatchTests
{
    [Fact]
    public void ApplyFilter_OnIQueryableOfDataRow_ProducesSameRowsAsBeforeRefactor()
    {
        // Two-row table; WHERE ID = 1 should keep only the first row.
        var (dataSets, sqlSelect) = BuildSelectWithSimpleEqualityFilter("Customers", "ID", 1);

        var engine = new QueryEngine(dataSets, sqlSelect);
        var result = engine.QueryAsDataTable();

        Assert.Equal(1, result.Rows.Count);
        Assert.Equal(1, Convert.ToInt32(result.Rows[0]["ID"]));
        Assert.Equal("Alpha", (string)result.Rows[0]["Name"]);
    }

    [Fact]
    public void ToDataRows_OnIQueryableOfDataRow_ReturnsAllRowsWhenNoWhereClause()
    {
        // No WHERE clause → engine takes the ToDataRows path (not ApplyFilter).
        var dataSets = new[] { BuildTwoRowDataSet() };
        var sqlSelect = BuildSelectAll("Customers");

        var engine = new QueryEngine(dataSets, sqlSelect);
        var result = engine.QueryAsDataTable();

        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public void ApplyFilter_RepeatedCallsForSameElementType_ReuseCachedDelegate()
    {
        // Force the cache to a known state, then call ApplyFilter twice for the same
        // element type and confirm the per-element-type cache grew by exactly 1.
        //
        // Issue #188: ApplyFilter has two element-type caches now — the legacy
        // ApplyFilterCacheCount (used for non-cacheable shapes like BETWEEN/CASE/IN) and
        // the new ApplyFilterCachedPredicateCacheCount (Wave 14 fast path for cacheable
        // shapes like simple binary comparisons). The simple-equality filter built here
        // is a cacheable shape, so the new cache is the one to assert against.
        CompiledQueryDispatch.ClearForTests();
        Assert.Equal(0, CompiledQueryDispatch.ApplyFilterCacheCount);
        Assert.Equal(0, CompiledQueryDispatch.ApplyFilterCachedPredicateCacheCount);

        var (dataSets, sqlSelect) = BuildSelectWithSimpleEqualityFilter("Customers", "ID", 1);

        // First call → compiles a delegate for IQueryable<DataRow> on the fast path.
        new QueryEngine(dataSets, sqlSelect).QueryAsDataTable();
        var afterFirstCall = CompiledQueryDispatch.ApplyFilterCachedPredicateCacheCount;

        // Second call → should hit the cache, no new compile.
        new QueryEngine(dataSets, sqlSelect).QueryAsDataTable();
        var afterSecondCall = CompiledQueryDispatch.ApplyFilterCachedPredicateCacheCount;

        Assert.Equal(1, afterFirstCall);
        Assert.Equal(afterFirstCall, afterSecondCall);

        // The legacy cache is untouched because the predicate is a cacheable shape.
        Assert.Equal(0, CompiledQueryDispatch.ApplyFilterCacheCount);
    }

    [Fact]
    public void ToDataRows_RepeatedCallsForSameElementType_ReuseCachedDelegate()
    {
        CompiledQueryDispatch.ClearForTests();
        Assert.Equal(0, CompiledQueryDispatch.ToDataRowsCacheCount);

        var dataSets = new[] { BuildTwoRowDataSet() };
        var sqlSelect = BuildSelectAll("Customers");

        new QueryEngine(dataSets, sqlSelect).QueryAsDataTable();
        var afterFirstCall = CompiledQueryDispatch.ToDataRowsCacheCount;

        new QueryEngine(dataSets, sqlSelect).QueryAsDataTable();
        var afterSecondCall = CompiledQueryDispatch.ToDataRowsCacheCount;

        Assert.Equal(1, afterFirstCall);
        Assert.Equal(afterFirstCall, afterSecondCall);
    }

    [Fact]
    public void Concurrent_ParallelQueries_ProduceCorrectResultsWithoutExceptions()
    {
        // Hammer the cache from multiple threads in parallel. A non-thread-safe cache
        // would race the GetOrAdd or compile, surfacing as exceptions or wrong counts.
        // 1024 iterations is enough to make any race condition observable on a 4-core box.
        const int Iterations = 1024;
        const int Parallelism = 8;

        // Don't ClearForTests here — a real consumer never clears the cache, so this
        // test should pass under both warm and cold conditions. Run the body identically
        // either way.
        var dataSets = new[] { BuildTwoRowDataSet() };

        var sqlSelectFiltered = BuildSelectWithSimpleEqualityFilter(dataSets, "Customers", "ID", 1);
        var sqlSelectUnfiltered = BuildSelectAll("Customers");

        var exceptions = new ConcurrentBag<Exception>();
        var rowCounts = new ConcurrentBag<int>();

        var threads = new List<Thread>();
        for (int t = 0; t < Parallelism; t++)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < Iterations; i++)
                    {
                        // Alternate between filtered and unfiltered to exercise both
                        // ApplyFilter and ToDataRows code paths concurrently.
                        var sqlSelect = (i % 2 == 0) ? sqlSelectFiltered : sqlSelectUnfiltered;
                        var engine = new QueryEngine(dataSets, sqlSelect);
                        var result = engine.QueryAsDataTable();
                        rowCounts.Add(result.Rows.Count);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
            thread.Start();
            threads.Add(thread);
        }

        foreach (var thread in threads)
            thread.Join();

        Assert.Empty(exceptions);

        // Half the iterations were filtered (1 row), half unfiltered (2 rows).
        var totalRowsExpected = Parallelism * (Iterations / 2 * 1 + Iterations / 2 * 2);
        Assert.Equal(totalRowsExpected, rowCounts.Sum());
    }

    [Fact]
    public void CompiledDispatch_DoesNotWrapExceptionsInTargetInvocationException()
    {
        // The pre-#129 reflection dispatch threw TargetInvocationException whenever the
        // inner generic method threw. The compiled-delegate path must surface the inner
        // exception directly — that was the second main reason for #129.
        //
        // We trigger an exception by giving the WHERE clause a non-existent column ref.
        // (BuildExpression resolves the column via reflection on the row type and
        // throws if the property/column is missing.)

        var dataSets = new[] { BuildTwoRowDataSet() };

        SqlTable customersTable = new("MyDB", "Customers");

        // The WHERE-clause column points at a column that does NOT exist on the
        // backing DataTable. BuildExpression resolves the column at evaluation time
        // and throws — that exception must surface unwrapped (no
        // TargetInvocationException), which is the second motivator for #129.
        SqlColumn missingColumn = new("MyDB", customersTable.TableName, "NoSuchColumn")
        {
            ColumnType = typeof(int),
            TableRef = customersTable
        };
        SqlColumnRef missingColumnRef = new(null, customersTable.TableName, missingColumn.ColumnName)
        {
            Column = missingColumn
        };

        var sqlSelect = new SqlSelectDefinition();
        SqlColumn idCol = new("MyDB", customersTable.TableName, "ID")
        {
            ColumnType = typeof(int),
            TableRef = customersTable
        };
        sqlSelect.Columns.Add(idCol);
        sqlSelect.Table = customersTable;
        sqlSelect.WhereClause = new SqlExpression(new SqlBinaryExpression(
            new(missingColumnRef),
            SqlBinaryOperator.Equal,
            new(new SqlLiteralValue(1))));

        var engine = new QueryEngine(dataSets, sqlSelect);

        var ex = Record.Exception(() => engine.QueryAsDataTable());
        Assert.NotNull(ex);

        // The exception must NOT be a TargetInvocationException — that's the wrapper
        // MethodInfo.Invoke produces and the whole point of moving off reflection.
        Assert.IsNotType<System.Reflection.TargetInvocationException>(ex);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static (IEnumerable<DataSet> DataSets, SqlSelectDefinition SqlSelect) BuildSelectWithSimpleEqualityFilter(
        string tableName, string columnName, int filterValue)
    {
        var dataSets = new[] { BuildTwoRowDataSet() };
        return (dataSets, BuildSelectWithSimpleEqualityFilter(dataSets, tableName, columnName, filterValue));
    }

    private static SqlSelectDefinition BuildSelectWithSimpleEqualityFilter(
        IEnumerable<DataSet> dataSets, string tableName, string columnName, int filterValue)
    {
        // SELECT ID, Name FROM Customers WHERE ID = <filterValue>
        var sqlSelect = new SqlSelectDefinition();
        SqlTable customersTable = new("MyDB", tableName);

        SqlColumn idCol = new("MyDB", tableName, "ID") { ColumnType = typeof(int), TableRef = customersTable };
        SqlColumn nameCol = new("MyDB", tableName, "Name") { ColumnType = typeof(string), TableRef = customersTable };
        sqlSelect.Columns.Add(idCol);
        sqlSelect.Columns.Add(nameCol);
        sqlSelect.Table = customersTable;

        SqlColumn filterColumn = new("MyDB", tableName, columnName)
        {
            ColumnType = typeof(int),
            TableRef = customersTable
        };
        SqlColumnRef filterColumnRef = new(null, tableName, filterColumn.ColumnName)
        {
            Column = filterColumn
        };
        sqlSelect.WhereClause = new SqlExpression(new SqlBinaryExpression(
            new(filterColumnRef),
            SqlBinaryOperator.Equal,
            new(new SqlLiteralValue(filterValue))));

        return sqlSelect;
    }

    private static SqlSelectDefinition BuildSelectAll(string tableName)
    {
        var sqlSelect = new SqlSelectDefinition();
        SqlTable customersTable = new("MyDB", tableName);

        SqlColumn idCol = new("MyDB", tableName, "ID") { ColumnType = typeof(int), TableRef = customersTable };
        SqlColumn nameCol = new("MyDB", tableName, "Name") { ColumnType = typeof(string), TableRef = customersTable };
        sqlSelect.Columns.Add(idCol);
        sqlSelect.Columns.Add(nameCol);
        sqlSelect.Table = customersTable;
        return sqlSelect;
    }

    private static DataSet BuildTwoRowDataSet()
    {
        var dataSet = new DataSet("MyDB");
        var customers = new DataTable("Customers");
        customers.Columns.Add("ID", typeof(int));
        customers.Columns.Add("Name", typeof(string));
        customers.Rows.Add(1, "Alpha");
        customers.Rows.Add(2, "Beta");
        dataSet.Tables.Add(customers);
        return dataSet;
    }
}
