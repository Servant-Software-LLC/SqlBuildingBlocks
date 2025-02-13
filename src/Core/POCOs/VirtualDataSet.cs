using System.Data;

namespace SqlBuildingBlocks.POCOs;

public class VirtualDataSet
{
    public VirtualDataSet() { }
    public VirtualDataSet(DataSet dataSet)
    {
        foreach(DataTable dataTable in dataSet.Tables)
        {
            var virtualTable = new VirtualDataTable(dataTable);
            Tables.Add(dataTable.TableName, virtualTable);
        }
    }

    public IDictionary<string, VirtualDataTable>? Tables { get; } = new Dictionary<string, VirtualDataTable>();
}

