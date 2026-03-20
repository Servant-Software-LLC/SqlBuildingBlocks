namespace SqlBuildingBlocks.LogicalEntities;

public class SqlCreateIndexDefinition
{
    /// <summary>
    /// The name of the index.
    /// </summary>
    public string? IndexName { get; set; }

    /// <summary>
    /// Whether this is a UNIQUE index.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// The table the index is created on.
    /// </summary>
    public SqlTable? Table { get; set; }

    /// <summary>
    /// The columns included in the index.
    /// </summary>
    public IList<SqlOrderByColumn> Columns { get; private set; } = new List<SqlOrderByColumn>();

    /// <summary>
    /// Optional WHERE clause for partial indexes (PostgreSQL).
    /// </summary>
    public SqlExpression? WhereClause { get; set; }
}
