using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlExistsExpression
{
    public SqlExistsExpression(SqlSelectDefinition selectDefinition, bool isNegated)
    {
        SelectDefinition = selectDefinition ?? throw new ArgumentNullException(nameof(selectDefinition));
        IsNegated = isNegated;
    }

    public SqlSelectDefinition SelectDefinition { get; }
    public bool IsNegated { get; }

    public void Accept(ISqlExpressionVisitor visitor)
    {
        visitor.Visit(this);
        SelectDefinition.AcceptBinaryExpressions(visitor);
    }

    public string ToExpressionString()
    {
        var keyword = IsNegated ? "NOT EXISTS" : "EXISTS";
        return $"{keyword} ({SelectDefinition})";
    }

    public override string ToString() => ToExpressionString();
}
