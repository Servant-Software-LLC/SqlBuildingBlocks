using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities.BaseClasses;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlColumn : SqlColumnBase, ISqlColumn, ISqlColumnWithAlias
{
    public Type? ColumnType { get; set; }
    public SqlTable? TableRef { get; set; }

    public string? ColumnAlias { get; set; }

    public SqlColumn(string? databaseName, string? tableName, string columnName)
        : base(databaseName, tableName, columnName)
    {
    }

    public SqlColumnRef ToColumnRef() => new SqlColumnRef(DatabaseName, TableName, ColumnName) { Column = this };

    public override string ToString() =>
        string.IsNullOrEmpty(ColumnAlias) ? base.ToString() : base.ToString() + " AS " + ColumnAlias;
}
