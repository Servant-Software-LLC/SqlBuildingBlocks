using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlScalarSubqueryColumn : ISqlColumn, ISqlColumnWithAlias
{
    public SqlScalarSubqueryColumn(SqlSelectDefinition selectDefinition)
    {
        SelectDefinition = selectDefinition ?? throw new ArgumentNullException(nameof(selectDefinition));
    }

    public SqlSelectDefinition SelectDefinition { get; }
    public string? ColumnAlias { get; set; }
    public Type? ColumnType { get; set; }

    public string? ColumnName => ColumnAlias ?? SelectDefinition.Columns.FirstOrDefault()?.ColumnName;

    public override string ToString() => ColumnAlias == null ? $"({SelectDefinition})" : $"({SelectDefinition}) AS {ColumnAlias}";
}
