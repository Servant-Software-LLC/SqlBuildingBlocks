namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents a single item in a PostgreSQL RETURNING clause.
/// Can be a wildcard (*), a column reference, or an expression with an optional alias.
/// </summary>
public class SqlReturningItem
{
    /// <summary>
    /// Creates a wildcard RETURNING item (RETURNING *).
    /// </summary>
    public SqlReturningItem()
    {
        IsWildcard = true;
    }

    /// <summary>
    /// Creates a RETURNING item from an expression with an optional alias.
    /// </summary>
    public SqlReturningItem(SqlExpression expression, string? alias = null)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Alias = alias;
    }

    /// <summary>
    /// True if this item represents RETURNING *.
    /// </summary>
    public bool IsWildcard { get; }

    /// <summary>
    /// The expression being returned (column, function call, arithmetic, etc.).
    /// Null when <see cref="IsWildcard"/> is true.
    /// </summary>
    public SqlExpression? Expression { get; }

    /// <summary>
    /// Optional alias (e.g., RETURNING id AS user_id).
    /// </summary>
    public string? Alias { get; }
}
