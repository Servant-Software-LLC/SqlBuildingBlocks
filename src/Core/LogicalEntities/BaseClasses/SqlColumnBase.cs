namespace SqlBuildingBlocks.LogicalEntities.BaseClasses;

public abstract class SqlColumnBase
{
    public string? DatabaseName { get; set; }
    public string? TableName { get; set; }
    public string ColumnName { get; private set; }

    public SqlColumnBase(string? databaseName, string? tableName, string columnName)
    {
        DatabaseName = databaseName;
        TableName = tableName;
        ColumnName = columnName;
    }

    public override string ToString() =>
        !string.IsNullOrEmpty(DatabaseName) ? $"{DatabaseName}.{TableName}.{ColumnName}" :
        !string.IsNullOrEmpty(TableName) ? $"{TableName}.{ColumnName}" :
        $"{ColumnName}";
}
