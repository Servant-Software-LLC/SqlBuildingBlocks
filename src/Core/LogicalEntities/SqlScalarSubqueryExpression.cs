using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlScalarSubqueryExpression
{
    public SqlScalarSubqueryExpression(SqlSelectDefinition selectDefinition)
    {
        SelectDefinition = selectDefinition ?? throw new ArgumentNullException(nameof(selectDefinition));
    }

    public SqlSelectDefinition SelectDefinition { get; }
    public Type? ValueType { get; set; }

    public void Accept(ISqlExpressionVisitor visitor)
    {
        visitor.Visit(this);
        SelectDefinition.AcceptColumnExpressions(visitor);
        SelectDefinition.AcceptBinaryExpressions(visitor);
    }

    public string ToExpressionString() => $"({SelectDefinition})";

    public override string ToString() => ToExpressionString();
}
