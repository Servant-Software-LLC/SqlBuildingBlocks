namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents a single column reference in an ORDER BY clause, along with its sort direction.
/// </summary>
public class SqlOrderByColumn
{
    public SqlOrderByColumn(string columnName, bool descending = false)
    {
        ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
        Descending = descending;
    }

    /// <summary>
    /// The name of the column to sort by.  May be table-qualified (e.g. "t.Name").
    /// </summary>
    public string ColumnName { get; }

    /// <summary>
    /// <c>true</c> = DESC order; <c>false</c> (default) = ASC order.
    /// </summary>
    public bool Descending { get; }
}
