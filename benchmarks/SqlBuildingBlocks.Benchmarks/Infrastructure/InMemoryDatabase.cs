using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.QueryProcessing;
using System.Data;

namespace SqlBuildingBlocks.Benchmarks.Infrastructure;

/// <summary>
/// Lightweight synthetic table provider used by QueryEngine benchmarks.
///
/// Mirrors the integration-test InMemoryDatabase but lives inside the benchmark project
/// so the benchmarks do not take a project reference on test code (which would inflate
/// the benchmark assembly with test framework dependencies).
///
/// Tables are backed by <see cref="DataTable"/>; rows are exposed as IQueryable&lt;DataRow&gt;.
/// </summary>
internal sealed class InMemoryDatabase : ITableDataProvider, IDatabaseConnectionProvider
{
    private readonly DataSet dataSet;

    public InMemoryDatabase(string databaseName)
    {
        dataSet = new DataSet(databaseName);
    }

    public string DefaultDatabase => dataSet.DataSetName;

    public bool CaseInsensitive => true;

    public DataTable AddTable(string tableName, params (string ColumnName, Type ColumnType)[] columns)
    {
        var table = new DataTable(tableName);
        foreach (var (name, type) in columns)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                table.Columns.Add(new DataColumn(name, type.GenericTypeArguments[0]) { AllowDBNull = true });
            else
                table.Columns.Add(name, type);
        }

        dataSet.Tables.Add(table);
        return table;
    }

    public IEnumerable<DataColumn> GetColumns(SqlTable table)
    {
        var dt = ResolveTable(table)
            ?? throw new KeyNotFoundException($"Table '{table.TableName}' is not registered.");
        return dt.Columns.Cast<DataColumn>();
    }

    public IQueryable? GetTableData(SqlTable table)
    {
        var dt = ResolveTable(table);
        return dt?.Rows.Cast<DataRow>().AsQueryable();
    }

    public (bool DatabaseServiced, IEnumerable<SqlTableInfo> Tables) GetTables(string? database)
    {
        if (!string.IsNullOrEmpty(database) &&
            !string.Equals(database, dataSet.DataSetName, StringComparison.OrdinalIgnoreCase))
        {
            return (false, Array.Empty<SqlTableInfo>());
        }

        var tables = dataSet.Tables.Cast<DataTable>()
            .Select(t => new SqlTableInfo(new SqlTable(dataSet.DataSetName, t.TableName)));
        return (true, tables);
    }

    private DataTable? ResolveTable(SqlTable sqlTable)
    {
        if (!string.IsNullOrEmpty(sqlTable.DatabaseName) &&
            !string.Equals(sqlTable.DatabaseName, dataSet.DataSetName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return dataSet.Tables.Contains(sqlTable.TableName) ? dataSet.Tables[sqlTable.TableName] : null;
    }
}
