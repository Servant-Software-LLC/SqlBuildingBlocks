namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents a PostgreSQL RETURNING clause that can contain multiple return items.
/// Supports RETURNING *, RETURNING col1, col2, and RETURNING expr AS alias.
/// </summary>
public class SqlReturningClause
{
    public IList<SqlReturningItem> Items { get; } = new List<SqlReturningItem>();
}
