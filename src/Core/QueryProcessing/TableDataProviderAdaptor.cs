using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using System.Data;

namespace SqlBuildingBlocks.QueryProcessing;

public class TableDataProviderAdaptor : ITableDataProvider
{
    private readonly Dictionary<string, DataSet> dictDataSets = new();

    public TableDataProviderAdaptor(IEnumerable<DataSet> dataSets)
    {
        foreach (var dataSet in dataSets)
        {
            if (string.IsNullOrWhiteSpace(dataSet.DataSetName))
                throw new ArgumentException($"Dataset must have a name", nameof(dataSet.DataSetName));

            if (!dictDataSets.TryAdd(dataSet.DataSetName, dataSet))
                throw new ArgumentException($"A DataSet with the name {dataSet.DataSetName} already exists.", nameof(dataSets));
        }
    }

    public IEnumerable<DataColumn> GetColumns(SqlTable table)
    {
        if (table is null)
            throw new ArgumentNullException(nameof(table));

        if (string.IsNullOrEmpty(table.DatabaseName))
            throw new ArgumentException($"DatabaseName must be provided for the {nameof(table)} instance.", nameof(table.DatabaseName));

        if (!dictDataSets.TryGetValue(table.DatabaseName!, out var dataSet))
            throw new ArgumentException($"Dataset '{table.DatabaseName}' was not found", nameof(table.DatabaseName));

        if (!dataSet.Tables.Contains(table.TableName))
            throw new ArgumentException($"Table '{table.TableName}' was not found in DataSet '{table.DatabaseName}'", nameof(table.TableName));

        var dataTable = dataSet.Tables[table.TableName];

        return dataTable.Columns.OfType<DataColumn>();
    }

    public IQueryable? GetTableData(SqlTable sqlTable)
    {
        if (sqlTable is null)
            throw new ArgumentNullException(nameof(sqlTable));

        if (sqlTable.DatabaseName is null)
            throw new ArgumentException($"DatabaseName must be provided for the {nameof(sqlTable)} instance.", nameof(sqlTable.DatabaseName));

        if (!dictDataSets.TryGetValue(sqlTable.DatabaseName, out var dataSet))
            throw new ArgumentException($"Dataset '{sqlTable.DatabaseName}' was not found", nameof(sqlTable.DatabaseName));

        if (!dataSet.Tables.Contains(sqlTable.TableName))
            throw new ArgumentException($"Table '{sqlTable.TableName}' was not found in DataSet '{sqlTable.DatabaseName}'", nameof(sqlTable.TableName));

        var table = dataSet.Tables[sqlTable.TableName];
        return (from DataRow row in table.Rows select (object)row).AsQueryable();
    }

    public (bool DatabaseServiced, IEnumerable<SqlTableInfo> Tables) GetTables(string? database)
    {
        if (string.IsNullOrEmpty(database))
            throw new ArgumentNullException(nameof(database));

        if (!dictDataSets.TryGetValue(database!, out var dataSet))
            throw new ArgumentException($"Dataset '{database}' was not found", nameof(database));

        var tables = dataSet.Tables.OfType<DataTable>().Select(t => new SqlTableInfo(new SqlTable(database, t.TableName)));

        return (true, tables);
    }
}
