using System.Data;

namespace SqlBuildingBlocks.POCOs;

public class VirtualDataSet : IDisposable
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

    public void RemoveWithDisposal(string key)
    {
        if (tables.TryGetValue(key, out var table))
        {
            if (table is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Tables.Remove(key);
        }
    }

    /// <summary>
    /// Disposes all disposable virtual tables created by this VirtualDataSet.
    /// </summary>
    public void Dispose()
    {
        foreach (var table in tables)
        {
            if (table.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

}

