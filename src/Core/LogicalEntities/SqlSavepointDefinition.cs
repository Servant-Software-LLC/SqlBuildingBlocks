namespace SqlBuildingBlocks.LogicalEntities;

public class SqlSavepointDefinition
{
    /// <summary>
    /// The kind of savepoint statement (Create, Release, Rollback).
    /// </summary>
    public SqlSavepointKind Kind { get; set; }

    /// <summary>
    /// The name of the savepoint.
    /// </summary>
    public string? Name { get; set; }
}
