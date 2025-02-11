using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.QueryProcessing;
using System.Data;

namespace SqlBuildingBlocks.Core.Tests.Utils;

/// <summary>
/// Provides a never-ending stream of table data rows to the caller.  Useful to test if the QueryEngine
/// can handle large datasets without loading all the data of the table into memory.
/// </summary>
internal class UnendingTableDataProvider : ITableDataProvider
{
    internal const string databaseName = "MyDatabase";
    internal const string tableName = "locations";
    private readonly DataTable dataTable;
    public UnendingTableDataProvider()
    {
        dataTable = new DataTable();
        dataTable.Columns.Add("id", typeof(int));
        dataTable.Columns.Add("city", typeof(string));
        dataTable.Columns.Add("state", typeof(string));
        dataTable.Columns.Add("zip", typeof(int));

    }

    public int DataRowsProvided { get; private set; } = 0;

    public IEnumerable<DataColumn> GetColumns(SqlTable table)
    {
        if (string.Compare(table.TableName, tableName) == 0)
        {
            return dataTable.Columns.Cast<DataColumn>();
        }

        throw new KeyNotFoundException();
    }

    private IEnumerable<DataRow> DataRow_GetEnumerable()
    {
        while (true)
        {
            var newRow = dataTable.NewRow();
            newRow[0] = DataRowsProvided;
            newRow[1] = $"City#{DataRowsProvided}";
            newRow[1] = $"State#{DataRowsProvided}";
            newRow[1] = 10000 + DataRowsProvided;

            DataRowsProvided++;

            yield return newRow;
        }
    }

    public IQueryable GetTableData(SqlTable table)
    {
        if (string.Compare(table.TableName, tableName) == 0)
        {
            return DataRow_GetEnumerable().AsQueryable();
        }

        throw new KeyNotFoundException();
    }

    public (bool DatabaseServiced, IEnumerable<SqlTableInfo> Tables) GetTables(string database)
    {
        return (true, new SqlTableInfo[] { new SqlTableInfo(new SqlTable(databaseName, tableName)) });
    }
}
