using System.Data;

namespace SqlBuildingBlocks.POCOs;

/// <summary>
/// In order for us to support large data sources, the results coming from the QueryEngine must be in an enumerable iterator.  This iterator
/// will only pull rows from the data sources on demand.  Previously, we stored a whole DataTable of results which would require loading
/// all data sources and the whole result set into memory.
/// </summary>
public class VirtualDataTable
{
    public VirtualDataTable(string tableName) => 
        TableName = !string.IsNullOrEmpty(tableName) ? tableName : throw new ArgumentNullException(nameof(tableName));

    public VirtualDataTable(DataTable dataTable)
    {
        TableName = dataTable.TableName;
        Columns = dataTable.Columns;
        Rows = dataTable.Rows.Cast<DataRow>();
    }

    public string TableName { get; }
    public DataColumnCollection? Columns { get; set; }
    public IEnumerable<DataRow>? Rows { get; set; }

    public DataTable CreateEmptyDataTable()
    {
        DataTable table = new DataTable();
        SetSchema(table);

        return table;
    }

    /// <summary>
    /// Make a full copy of the schema and data contained in this virtual data table.  
    /// </summary>
    /// <remarks>Be careful, this call will copy of the data into memory.  The enumerable of <see cref="Rows"/> isn't necessarily in-memory.</remarks>
    /// <returns></returns>
    public DataTable ToDataTable()
    {
        DataTable table = new DataTable();
        SetSchema(table);

        if (Rows != null)
        {
            //Copy the data.
            foreach (DataRow dataRow in Rows)
            {
                table.Rows.Add(dataRow.ItemArray);
            }
        }

        return table;
    }

    private void SetSchema(DataTable table)
    {
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
                };

                table.Columns.Add(newCol);
            }
        }
    }
}
