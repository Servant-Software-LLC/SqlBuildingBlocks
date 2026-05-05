using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents a SELECT column whose value is a literal (post-resolution).
/// Produced by <see cref="SqlSelectDefinition.AcceptColumns"/> when a
/// <see cref="SqlParameterColumn"/>, <see cref="SqlFunctionColumn"/>, or
/// <see cref="SqlLiteralValueColumn"/> is resolved by a visitor (e.g.
/// <c>ResolveParametersVisitor</c>, <c>ResolveFunctionsVisitor</c>).
/// </summary>
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
