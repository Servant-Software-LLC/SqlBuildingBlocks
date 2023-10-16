namespace SqlBuildingBlocks.LogicalEntities;

public class SqlUniqueConstraint
{
    public IList<string> Columns { get; } = new List<string>();
}
