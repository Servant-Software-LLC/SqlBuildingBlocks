using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

//TODO:  This class is currently a placeholder and isn't used anywhere yet.
public class SqlLiteralValueColumn : ISqlColumn, ISqlColumnWithAlias
{
    public SqlLiteralValueColumn(SqlLiteralValue literalValue)
    {
        Value = literalValue ?? throw new ArgumentNullException(nameof(literalValue));
    }

    public SqlLiteralValue Value { get; set; }

    public string? ColumnAlias { get; set; }

    public string? ColumnName => null;
}
