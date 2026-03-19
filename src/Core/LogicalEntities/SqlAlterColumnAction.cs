namespace SqlBuildingBlocks.LogicalEntities;

public class SqlAlterColumnAction
{
    public SqlAlterColumnAction(string sourceColumnName, SqlColumnDefinition column)
    {
        SourceColumnName = !string.IsNullOrEmpty(sourceColumnName) ? sourceColumnName : throw new ArgumentNullException(nameof(sourceColumnName));
        Column = column ?? throw new ArgumentNullException(nameof(column));
    }

    public string SourceColumnName { get; }

    public SqlColumnDefinition Column { get; }
}
