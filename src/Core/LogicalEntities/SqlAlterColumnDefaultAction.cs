namespace SqlBuildingBlocks.LogicalEntities;

public enum SqlAlterColumnDefaultOperation
{
    SetDefault,
    DropDefault
}

public class SqlAlterColumnDefaultAction
{
    public SqlAlterColumnDefaultAction(string sourceColumnName, SqlAlterColumnDefaultOperation operation, SqlLiteralValue? defaultLiteralValue = null, SqlFunction? defaultFunctionValue = null)
    {
        SourceColumnName = !string.IsNullOrEmpty(sourceColumnName) ? sourceColumnName : throw new ArgumentNullException(nameof(sourceColumnName));
        Operation = operation;

        if (operation == SqlAlterColumnDefaultOperation.SetDefault)
        {
            if ((defaultLiteralValue == null) == (defaultFunctionValue == null))
                throw new ArgumentException("SetDefault requires exactly one default value.", nameof(defaultLiteralValue));
        }
        else if (defaultLiteralValue != null || defaultFunctionValue != null)
        {
            throw new ArgumentException("DropDefault cannot carry a default value.");
        }

        DefaultLiteralValue = defaultLiteralValue;
        DefaultFunctionValue = defaultFunctionValue;
    }

    public string SourceColumnName { get; }

    public SqlAlterColumnDefaultOperation Operation { get; }

    public SqlLiteralValue? DefaultLiteralValue { get; }

    public SqlFunction? DefaultFunctionValue { get; }
}
