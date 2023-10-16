namespace SqlBuildingBlocks.LogicalEntities;

public class SqlPrimaryKeyConstraint
{
    public IList<string> Columns { get; } = new List<string>();
}
