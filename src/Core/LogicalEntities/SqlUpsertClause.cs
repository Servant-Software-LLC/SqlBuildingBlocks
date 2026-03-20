namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents an upsert clause on an INSERT statement.
/// PostgreSQL: ON CONFLICT (columns) DO UPDATE SET ... / DO NOTHING
/// MySQL: ON DUPLICATE KEY UPDATE ...
/// </summary>
public class SqlUpsertClause
{
    public SqlUpsertAction Action { get; set; }

    /// <summary>
    /// The conflict target columns (PostgreSQL ON CONFLICT (col1, col2)).
    /// Empty for MySQL ON DUPLICATE KEY UPDATE or when using ON CONSTRAINT.
    /// </summary>
    public IList<SqlColumn> ConflictColumns { get; } = new List<SqlColumn>();

    /// <summary>
    /// The constraint name for ON CONFLICT ON CONSTRAINT constraint_name.
    /// Null when using column-based conflict target.
    /// </summary>
    public string? ConstraintName { get; set; }

    /// <summary>
    /// Optional WHERE clause on the conflict target (partial index filter).
    /// PostgreSQL: ON CONFLICT (col) WHERE condition DO ...
    /// </summary>
    public SqlExpression? ConflictTargetWhereCondition { get; set; }

    /// <summary>
    /// Optional WHERE clause on the DO UPDATE SET action.
    /// PostgreSQL: ON CONFLICT (col) DO UPDATE SET ... WHERE condition
    /// </summary>
    public SqlExpression? WhereCondition { get; set; }

    /// <summary>
    /// The SET assignments to apply on conflict (when Action is Update).
    /// </summary>
    public IList<SqlAssignment> Assignments { get; } = new List<SqlAssignment>();
}
