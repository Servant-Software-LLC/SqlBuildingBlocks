using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlBetweenExpression
{
    public SqlBetweenExpression(SqlExpression operand, SqlExpression lowerBound, SqlExpression upperBound, bool isNegated)
    {
        Operand = operand ?? throw new ArgumentNullException(nameof(operand));
        LowerBound = lowerBound ?? throw new ArgumentNullException(nameof(lowerBound));
        UpperBound = upperBound ?? throw new ArgumentNullException(nameof(upperBound));
        IsNegated = isNegated;
    }

    /// <summary>The expression being tested (left side of BETWEEN).</summary>
    public SqlExpression Operand { get; set; }

    /// <summary>The lower bound of the range.</summary>
    public SqlExpression LowerBound { get; set; }

    /// <summary>The upper bound of the range.</summary>
    public SqlExpression UpperBound { get; set; }

    /// <summary>True when the predicate is NOT BETWEEN; false for plain BETWEEN.</summary>
    public bool IsNegated { get; set; }

    public void Accept(ISqlExpressionVisitor visitor)
    {
        visitor.Visit(this);
        Operand.Accept(visitor);
        LowerBound.Accept(visitor);
        UpperBound.Accept(visitor);
    }

    public string ToExpressionString()
    {
        var keyword = IsNegated ? "NOT BETWEEN" : "BETWEEN";
        return $"{Operand.ToExpressionString()} {keyword} {LowerBound.ToExpressionString()} AND {UpperBound.ToExpressionString()}";
    }

    public override string ToString()
    {
        var keyword = IsNegated ? "NOT BETWEEN" : "BETWEEN";
        return $"{Operand} {keyword} {LowerBound} AND {UpperBound}";
    }
}
