using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents a PostgreSQL array subscript expression, e.g. <c>col[1]</c> or <c>col[1:3]</c>.
/// </summary>
public class SqlArraySubscript
{
    public SqlArraySubscript(SqlExpression array, SqlExpression index)
    {
        Array = array ?? throw new ArgumentNullException(nameof(array));
        Index = index ?? throw new ArgumentNullException(nameof(index));
    }

    public SqlArraySubscript(SqlExpression array, SqlExpression lowerBound, SqlExpression upperBound)
    {
        Array = array ?? throw new ArgumentNullException(nameof(array));
        LowerBound = lowerBound ?? throw new ArgumentNullException(nameof(lowerBound));
        UpperBound = upperBound ?? throw new ArgumentNullException(nameof(upperBound));
        IsSlice = true;
    }

    public SqlExpression Array { get; }
    public SqlExpression? Index { get; }
    public SqlExpression? LowerBound { get; }
    public SqlExpression? UpperBound { get; }
    public bool IsSlice { get; }

    public void Accept(ISqlExpressionVisitor visitor)
    {
        visitor.Visit(this);
        Array.Accept(visitor);
        if (IsSlice)
        {
            LowerBound!.Accept(visitor);
            UpperBound!.Accept(visitor);
        }
        else
        {
            Index!.Accept(visitor);
        }
    }

    public string ToExpressionString()
    {
        if (IsSlice)
            return $"{Array.ToExpressionString()}[{LowerBound!.ToExpressionString()}:{UpperBound!.ToExpressionString()}]";
        return $"{Array.ToExpressionString()}[{Index!.ToExpressionString()}]";
    }

    public override string ToString() => ToExpressionString();
}
