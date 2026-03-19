namespace SqlBuildingBlocks.LogicalEntities;

public enum SqlAlterColumnOperation
{
    Alter,
    Modify,
    Change
}

public class SqlAlterColumnAction
{
    public SqlAlterColumnAction(string sourceColumnName, SqlColumnDefinition column, SqlAlterColumnOperation operation)
    {
        SourceColumnName = !string.IsNullOrEmpty(sourceColumnName) ? sourceColumnName : throw new ArgumentNullException(nameof(sourceColumnName));
        Column = column ?? throw new ArgumentNullException(nameof(column));
        Operation = operation;
    }

    public string SourceColumnName { get; }

    public SqlColumnDefinition Column { get; }

    public SqlAlterColumnOperation Operation { get; }
}
