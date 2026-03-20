namespace SqlBuildingBlocks.LogicalEntities;

public class SqlDropViewDefinition
{
    public bool IfExists { get; set; }

    public IList<SqlTable> Views { get; private set; } = new List<SqlTable>();
}
