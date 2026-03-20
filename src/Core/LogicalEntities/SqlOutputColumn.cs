namespace SqlBuildingBlocks.LogicalEntities;

public class SqlOutputColumn
{
    public SqlOutputColumn(string source, string? columnName)
    {
        Source = source;
        ColumnName = columnName;
    }

    /// <summary>
    /// The pseudo-table source: "INSERTED" or "DELETED".
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// The column name, or null if wildcard (*).
    /// </summary>
    public string? ColumnName { get; }

    public override string ToString() =>
        ColumnName == null ? $"{Source}.*" : $"{Source}.{ColumnName}";
}
