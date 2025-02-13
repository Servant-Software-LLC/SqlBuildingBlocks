using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.POCOs;
using System.Data;

namespace SqlBuildingBlocks.QueryProcessing;

public class TableDataProviderAdaptor : ITableDataProvider
{
    private readonly Dictionary<string, VirtualDataSet> dictDataSets = new();

    public TableDataProviderAdaptor() { }

    public TableDataProviderAdaptor(IEnumerable<DataSet> dataSets)
    {
        foreach (var dataSet in dataSets)
        {
            AddDataSet(dataSet);
        }
    }

    public void AddDataSet(DataSet dataSet)
    {
        if (string.IsNullOrWhiteSpace(dataSet.DataSetName))
            throw new ArgumentException($"Dataset must have a name", nameof(dataSet.DataSetName));

        var virtualDataSet = new VirtualDataSet(dataSet);
        AddDataSet(dataSet.DataSetName, virtualDataSet);
    }

    public void AddDataSet(string dataSetName, VirtualDataSet virtualDataSet)
    {
        if (!dictDataSets.TryAdd(dataSetName, virtualDataSet))
            throw new ArgumentException($"A DataSet with the name {dataSetName} already exists.", nameof(virtualDataSet));
    }

    public IEnumerable<DataColumn> GetColumns(SqlTable sqlTable)
    {
        if (sqlTable is null)
            throw new ArgumentNullException(nameof(sqlTable));

        if (string.IsNullOrEmpty(sqlTable.DatabaseName))
            throw new ArgumentException($"DatabaseName must be provided for the {nameof(sqlTable)} instance.", nameof(sqlTable.DatabaseName));

        if (!dictDataSets.TryGetValue(sqlTable.DatabaseName!, out var dataSet))
            throw new ArgumentException($"Dataset '{sqlTable.DatabaseName}' was not found", nameof(sqlTable.DatabaseName));

        if (dataSet.Tables == null || dataSet.Tables.Count == 0)
            throw new ArgumentNullException($"Dataset '{sqlTable.DatabaseName}' does not have any tables.");

        if (!dataSet.Tables.ContainsKey(sqlTable.TableName))
            throw new ArgumentException($"Table '{sqlTable.TableName}' was not found in DataSet '{sqlTable.DatabaseName}'", nameof(sqlTable.TableName));

        var dataTable = dataSet.Tables[sqlTable.TableName];

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

        if (dataSet.Tables == null || dataSet.Tables.Count == 0)
            throw new ArgumentNullException($"Dataset '{sqlTable.DatabaseName}' does not have any tables.");

        if (!dataSet.Tables.ContainsKey(sqlTable.TableName))
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
