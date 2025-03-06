using System.Data;

namespace SqlBuildingBlocks.POCOs;

public class VirtualDataSet
{
    private readonly IDictionary<string, VirtualDataTable> tables = new Dictionary<string, VirtualDataTable>(StringComparer.OrdinalIgnoreCase);

    public VirtualDataSet() { }
    public VirtualDataSet(DataSet dataSet)
    {
        foreach(DataTable dataTable in dataSet.Tables)
        {
            Tables.Add(dataTable);
        }
    }

    public VirtualDataTableCollection Tables => new VirtualDataTableCollection(tables);

}

