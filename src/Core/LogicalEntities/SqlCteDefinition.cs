namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents a single Common Table Expression (CTE) definition in a WITH clause.
/// e.g. WITH cte_name AS (SELECT ...)
/// </summary>
public class SqlCteDefinition
{
    public SqlCteDefinition(string name, SqlSelectDefinition selectDefinition)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SelectDefinition = selectDefinition ?? throw new ArgumentNullException(nameof(selectDefinition));
    }

    /// <summary>
    /// The name of the CTE.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The subquery that defines the CTE.
    /// </summary>
    public SqlSelectDefinition SelectDefinition { get; }

    public override string ToString() => $"{Name} AS (<subquery>)";
}
