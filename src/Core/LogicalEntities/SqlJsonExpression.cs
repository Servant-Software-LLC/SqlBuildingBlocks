using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents a PostgreSQL JSON/JSONB operator expression,
/// e.g. <c>data-&gt;'key'</c>, <c>data@&gt;'{"a":1}'</c>, <c>data?'key'</c>.
/// </summary>
public class SqlJsonExpression
{
    public SqlJsonExpression(SqlExpression left, SqlJsonOperator @operator, SqlExpression right)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Operator = @operator;
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public SqlExpression Left { get; }
    public SqlJsonOperator Operator { get; }
    public SqlExpression Right { get; }

    public void Accept(ISqlExpressionVisitor visitor)
    {
        visitor.Visit(this);
        Left.Accept(visitor);
        Right.Accept(visitor);
    }

    public static string OperatorToString(SqlJsonOperator op) => op switch
    {
        SqlJsonOperator.Arrow => "->",
        SqlJsonOperator.DoubleArrow => "->>",
        SqlJsonOperator.HashArrow => "#>",
        SqlJsonOperator.HashDoubleArrow => "#>>",
        SqlJsonOperator.Contains => "@>",
        SqlJsonOperator.ContainedBy => "<@",
        SqlJsonOperator.KeyExists => "?",
        SqlJsonOperator.AnyKeyExists => "?|",
        SqlJsonOperator.AllKeysExist => "?&",
        _ => throw new ArgumentException($"Unknown JSON operator: {op}", nameof(op))
    };

    public string ToExpressionString() =>
        $"{Left.ToExpressionString()} {OperatorToString(Operator)} {Right.ToExpressionString()}";

    public override string ToString() => ToExpressionString();
}
