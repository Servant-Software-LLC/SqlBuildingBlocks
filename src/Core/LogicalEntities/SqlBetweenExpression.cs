using SqlBuildingBlocks.Interfaces;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

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

    public Expression GetExpression(Dictionary<SqlTable, DataRow> substituteValues, SqlTable tableDataRow, ParameterExpression param)
    {
        // BETWEEN is equivalent to: operand >= lower AND operand <= upper
        // NOT BETWEEN is equivalent to: operand < lower OR operand > upper
        var operandVsLower = Operand.GetExpression(substituteValues, tableDataRow, param, LowerBound);
        var lowerExpr = LowerBound.GetExpression(substituteValues, tableDataRow, param, Operand);

        var operandVsUpper = Operand.GetExpression(substituteValues, tableDataRow, param, UpperBound);
        var upperExpr = UpperBound.GetExpression(substituteValues, tableDataRow, param, Operand);

        // Align types for comparison
        AlignTypes(ref operandVsLower, ref lowerExpr);
        AlignTypes(ref operandVsUpper, ref upperExpr);

        var greaterThanOrEqual = MakeComparison(operandVsLower, lowerExpr, ExpressionType.GreaterThanOrEqual);
        var lessThanOrEqual = MakeComparison(operandVsUpper, upperExpr, ExpressionType.LessThanOrEqual);

        var result = Expression.AndAlso(greaterThanOrEqual, lessThanOrEqual);

        return IsNegated ? Expression.Not(result) : result;
    }

    private static Expression MakeComparison(Expression left, Expression right, ExpressionType comparisonType)
    {
        // String types don't support >=, <= directly; use string.Compare instead
        if (left.Type == typeof(string) && right.Type == typeof(string))
        {
            MethodInfo compareMethod = typeof(string).GetMethod("Compare", new[] { typeof(string), typeof(string) })!;
            var compareCall = Expression.Call(compareMethod, left, right);
            var zero = Expression.Constant(0);
            return Expression.MakeBinary(comparisonType, compareCall, zero);
        }

        return Expression.MakeBinary(comparisonType, left, right);
    }

    private static void AlignTypes(ref Expression left, ref Expression right)
    {
        if (left.Type == right.Type)
            return;

        if (left.Type == typeof(object))
            left = Expression.Convert(left, right.Type);
        else if (right.Type == typeof(object))
            right = Expression.Convert(right, left.Type);
        else
        {
            // Convert the narrower type to the wider type
            try
            {
                right = Expression.Convert(right, left.Type);
            }
            catch
            {
                left = Expression.Convert(left, right.Type);
            }
        }
    }

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
