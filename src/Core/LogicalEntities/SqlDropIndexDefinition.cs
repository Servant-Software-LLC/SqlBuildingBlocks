namespace SqlBuildingBlocks.LogicalEntities;

public class SqlDropIndexDefinition
{
    public bool IfExists { get; set; }

    /// <summary>
    /// The name of the index to drop.
    /// </summary>
    public string? IndexName { get; set; }
}
