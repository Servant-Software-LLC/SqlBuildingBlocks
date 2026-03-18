namespace SqlBuildingBlocks.LogicalEntities;

public class SqlColumnDefinition
{
    public SqlColumnDefinition(string columnName, SqlDataType dataType)
    {
        ColumnName = !string.IsNullOrEmpty(columnName) ? columnName : throw new ArgumentNullException(nameof(columnName));
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
    }

    public string ColumnName { get; private set; }

    public SqlDataType DataType { get; set; }

    public bool AllowNulls { get; set; } = true;

    public SqlLiteralValue? DefaultLiteralValue { get; set; }

    public SqlFunction? DefaultFunctionValue { get; set; }

    public bool IsAutoIncrement { get; set; }

    public int? IdentitySeed { get; set; }

    public int? IdentityIncrement { get; set; }

}
