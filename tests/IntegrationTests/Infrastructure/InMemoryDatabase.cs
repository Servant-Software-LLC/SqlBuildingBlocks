using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.QueryProcessing;
using System.Data;

namespace SqlBuildingBlocks.IntegrationTests.Infrastructure;

/// <summary>
/// A synthetic in-memory database used as a stand-in for the real ITableDataProvider
/// implementations that downstream consumers (FileBased.DataProviders, MockDB) build.
/// Tables are backed by <see cref="DataTable"/>; rows are exposed as IQueryable&lt;DataRow&gt;.
///
/// This represents the way a consumer wires SqlBuildingBlocks into their host: define
/// a database name, register tables (schema + rows), then hand the provider to the
/// QueryEngine alongside the parsed SqlSelectDefinition.
/// </summary>
public sealed class InMemoryDatabase : ITableDataProvider, IDatabaseConnectionProvider
{
    private readonly DataSet dataSet;

    public InMemoryDatabase(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name is required.", nameof(databaseName));

        dataSet = new DataSet(databaseName);
    }

    public string DefaultDatabase => dataSet.DataSetName;

    public bool CaseInsensitive => true;

    /// <summary>
    /// Adds a table to the in-memory database. Returns the underlying DataTable so
    /// callers can append rows fluently.
    /// </summary>
    public DataTable AddTable(string tableName, params (string ColumnName, Type ColumnType)[] columns)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name is required.", nameof(tableName));
        if (columns is null || columns.Length == 0)
            throw new ArgumentException("At least one column is required.", nameof(columns));

        var table = new DataTable(tableName);
        foreach (var (name, type) in columns)
        {
            // DataColumn does not natively support Nullable<T>; unwrap so the schema
            // stores the underlying type and AllowDBNull = true.
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                table.Columns.Add(new DataColumn(name, type.GenericTypeArguments[0]) { AllowDBNull = true });
            }
            else
            {
                table.Columns.Add(name, type);
            }
        }

        dataSet.Tables.Add(table);
        return table;
    }

    public IEnumerable<DataColumn> GetColumns(SqlTable table)
    {
        if (table is null) throw new ArgumentNullException(nameof(table));
        var dt = ResolveTable(table);
        if (dt is null)
            throw new KeyNotFoundException($"Table '{table.TableName}' is not registered in database '{dataSet.DataSetName}'.");

        return dt.Columns.Cast<DataColumn>();
    }

    public IQueryable? GetTableData(SqlTable table)
    {
        if (table is null) throw new ArgumentNullException(nameof(table));
        var dt = ResolveTable(table);
        if (dt is null)
            return null;

        return dt.Rows.Cast<DataRow>().AsQueryable();
    }

    public (bool DatabaseServiced, IEnumerable<SqlTableInfo> Tables) GetTables(string? database)
    {
        // If the consumer asked about a different database, signal "not serviced" so
        // an outer AllTableDataProvider (if used) can keep searching.
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
        // Allow the SqlTable to omit a database name; default to the only registered DB.
        if (!string.IsNullOrEmpty(sqlTable.DatabaseName) &&
            !string.Equals(sqlTable.DatabaseName, dataSet.DataSetName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return dataSet.Tables.Contains(sqlTable.TableName) ? dataSet.Tables[sqlTable.TableName] : null;
    }
}
