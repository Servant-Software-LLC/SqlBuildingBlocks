using System.Data;

namespace SqlBuildingBlocks.POCOs;

public class VirtualDataSet
{
    private readonly IDictionary<string, VirtualDataTable> tables = new Dictionary<string, VirtualDataTable>();

    public VirtualDataSet() { }
    public VirtualDataSet(DataSet dataSet)
    {
        foreach(DataTable dataTable in dataSet.Tables)
        {
            AddTable(dataTable);
        }
    }

    public void AddTable(DataTable dataTable)
    {
        var virtualTable = new VirtualDataTable(dataTable);
        tables.Add(dataTable.TableName, virtualTable);
    }

    public VirtualDataTableCollection Tables => new VirtualDataTableCollection(tables);

}

