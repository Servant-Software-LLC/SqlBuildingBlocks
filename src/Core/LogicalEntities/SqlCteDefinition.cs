namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents a single Common Table Expression (CTE) definition in a WITH clause.
/// e.g. WITH cte_name AS (SELECT ...) or WITH RECURSIVE cte_name AS (anchor UNION ALL recursive)
/// </summary>
public class SqlCteDefinition
{
    public SqlCteDefinition(string name, SqlSelectDefinition selectDefinition, bool isRecursive = false)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SelectDefinition = selectDefinition ?? throw new ArgumentNullException(nameof(selectDefinition));
        IsRecursive = isRecursive;
    }

    /// <summary>
    /// The name of the CTE.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The subquery that defines the CTE.
    /// </summary>
    public SqlSelectDefinition SelectDefinition { get; }

    /// <summary>
    /// Whether this CTE was declared with the RECURSIVE keyword.
    /// </summary>
    public bool IsRecursive { get; }

    public override string ToString() => IsRecursive ? $"RECURSIVE {Name} AS (<subquery>)" : $"{Name} AS (<subquery>)";
}
