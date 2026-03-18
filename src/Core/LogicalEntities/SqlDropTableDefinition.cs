namespace SqlBuildingBlocks.LogicalEntities;

public class SqlDropTableDefinition
{
    public bool IfExists { get; set; }

    public IList<SqlTable> Tables { get; private set; } = new List<SqlTable>();
}
