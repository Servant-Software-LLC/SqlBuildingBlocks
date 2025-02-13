using System.Data;

namespace SqlBuildingBlocks.POCOs;


/// <summary>
/// In order for us to support large data sources, the results coming from the QueryEngine must be in an enumerable iterator.  This iterator
/// will only pull rows from the data sources on demand.  Previously, we stored a whole DataTable of results which would require loading
/// all data sources and the whole result set into memory.
/// </summary>
public class VirtualDataTable
{
    public VirtualDataTable() { }

    public VirtualDataTable(DataTable dataTable)
    {
        TableName = dataTable.TableName;
        Columns = dataTable.Columns;
        Rows = dataTable.Rows.Cast<DataRow>();
    }

    public string? TableName { get; set; }
    public DataColumnCollection? Columns { get; set; }
    public IEnumerable<DataRow>? Rows { get; set; }

    public DataTable CreateEmptyDataTable()
    {
        DataTable table = new DataTable();

        if (Columns != null)
        {
            foreach (DataColumn col in Columns)
            {
                // Create a new DataColumn with the same name and data type.
                DataColumn newCol = new DataColumn(col.ColumnName, col.DataType)
                {
                    Caption = col.Caption,
                    DefaultValue = col.DefaultValue,
                    ReadOnly = col.ReadOnly,
                    Unique = col.Unique
                    // You can copy additional properties as needed.
                };

                table.Columns.Add(newCol);
            }
        }

        return table;
    }
}
