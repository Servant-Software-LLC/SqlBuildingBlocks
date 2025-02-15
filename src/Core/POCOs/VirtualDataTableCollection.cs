using System.Collections;

namespace SqlBuildingBlocks.POCOs;

public class VirtualDataTableCollection : IEnumerable<VirtualDataTable>
{
    private readonly IDictionary<string, VirtualDataTable> virtualDataTables;
    internal VirtualDataTableCollection(IDictionary<string, VirtualDataTable> virtualDataTables) =>
        this.virtualDataTables = virtualDataTables ?? throw new ArgumentNullException(nameof(virtualDataTables));


    public VirtualDataTable this[string name] => virtualDataTables[name];

    public int Count => virtualDataTables.Count;

    public void Add(VirtualDataTable data) => virtualDataTables.Add(data.TableName!, data);
    public void Remove(string name) => virtualDataTables.Remove(name);

    public bool Contains(string name) => virtualDataTables.ContainsKey(name);

    /// <summary>
    /// Returns a generic enumerator that iterates through the VirtualDataTable values.
    /// </summary>
    public IEnumerator<VirtualDataTable> GetEnumerator() => virtualDataTables.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
