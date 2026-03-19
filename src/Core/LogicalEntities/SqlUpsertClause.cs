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
    /// Empty for MySQL ON DUPLICATE KEY UPDATE.
    /// </summary>
    public IList<SqlColumn> ConflictColumns { get; } = new List<SqlColumn>();

    /// <summary>
    /// The SET assignments to apply on conflict (when Action is Update).
    /// </summary>
    public IList<SqlAssignment> Assignments { get; } = new List<SqlAssignment>();
}
