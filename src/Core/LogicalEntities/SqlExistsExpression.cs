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
    public SqlSelectDefinition Subquery => SelectDefinition;
    public bool IsNegated { get; }

    public void Accept(ISqlExpressionVisitor visitor)
    {
        visitor.Visit(this);

        if (visitor is ISqlValueVisitor valueVisitor)
            SelectDefinition.AcceptColumns(valueVisitor);

        SelectDefinition.AcceptBinaryExpressions(visitor);
    }

    public string ToExpressionString() => IsNegated ? "NOT EXISTS (<subquery>)" : "EXISTS (<subquery>)";

    public override string ToString() => ToExpressionString();
}
