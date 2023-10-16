using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.Utils;
using System.Data;
using System.Linq.Expressions;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlBinaryExpression
{
    public SqlBinaryExpression(SqlExpression left, SqlBinaryOperator binaryOperator, SqlExpression right)
    {
        Left = left;
        Operator = binaryOperator;
        Right = right;
    }

    public SqlExpression Left { get; set; }
    public SqlBinaryOperator Operator { get; set; }
    public SqlExpression Right { get; set; }

    public void Accept(ISqlExpressionVisitor visitor)
    {
        visitor.Visit(this);
        Left.Accept(visitor);
        Right.Accept(visitor);
    }

    public Expression<Func<TDataRow, bool>> BuildExpression<TDataRow>(Dictionary<SqlTable, DataRow> substituteValues, SqlTable tableDataRow)
    {
        var param = Expression.Parameter(typeof(TDataRow), "dataRow");
        var lambdaExpression = Expression.Lambda<Func<TDataRow, bool>>(
                GetExpression(substituteValues, tableDataRow, param),
                param
            );

        return lambdaExpression;
    }

    public Expression GetExpression(Dictionary<SqlTable, DataRow> substituteValues, SqlTable tableDataRow, ParameterExpression param)
    {
        var leftProperty = Left.GetExpression(substituteValues, tableDataRow, param, Right);
        var rightProperty = Right.GetExpression(substituteValues, tableDataRow, param, Left);

        return Operator switch
        {
            SqlBinaryOperator.Equal => GetBinaryExpression(leftProperty, rightProperty, Expression.Equal),
            SqlBinaryOperator.LessThan => GetBinaryExpression(leftProperty, rightProperty, Expression.LessThan),
            SqlBinaryOperator.LessThanEqual => GetBinaryExpression(leftProperty, rightProperty, Expression.LessThanOrEqual),
            SqlBinaryOperator.GreaterThan => GetBinaryExpression(leftProperty, rightProperty, Expression.GreaterThan),
            SqlBinaryOperator.GreaterThanEqual => GetBinaryExpression(leftProperty, rightProperty, Expression.GreaterThanOrEqual),
            SqlBinaryOperator.And => Expression.And(leftProperty, rightProperty),
            SqlBinaryOperator.Or => Expression.Or(leftProperty, rightProperty),
            SqlBinaryOperator.Like => GetRegexIsMatchExpression(leftProperty, rightProperty),
            _ => throw new ArgumentException($"Invalid binary operator {Operator} in {nameof(GetExpression)}", nameof(Operator))
        };
    }



    private Expression GetRegexIsMatchExpression(Expression column, Expression likePattern)
    {
        var columnNullable = column.Type.IsGenericType && column.Type.GetGenericTypeDefinition() == typeof(Nullable<>);

        if (!columnNullable)
            return RegexLinqExpression.IsMatch(column, likePattern);

        var columnHasValue = Expression.Property(column, "HasValue");
        var columnValue = Expression.Property(column, "Value");
        var falseExpression = Expression.Constant(false, typeof(bool));
        var ternary = Expression.Condition(Expression.Not(columnHasValue), falseExpression,
                                           RegexLinqExpression.IsMatch(Expression.Convert(columnValue, column.Type), likePattern));
        return ternary;
    }

    private BinaryExpression GetBinaryExpression(Expression left, Expression right, Func<Expression, Expression, BinaryExpression> binaryOperatorExpressionFunc)
    {
        var leftNullable = left.Type.IsGenericType && left.Type.GetGenericTypeDefinition() == typeof(Nullable<>);
        var rightNullable = right.Type.IsGenericType && right.Type.GetGenericTypeDefinition() == typeof(Nullable<>);

        if (!leftNullable && !rightNullable)
            return binaryOperatorExpressionFunc(left, right);

        var leftHasValue = leftNullable ? Expression.Property(left, "HasValue") : null;
        var leftValue = leftNullable ? Expression.Property(left, "Value") : null;
        var leftNull = leftNullable ? Expression.Constant(null, left.Type) : null;
        var rightHasValue = rightNullable ? Expression.Property(right, "HasValue") : null;
        var rightValue = rightNullable ? Expression.Property(right, "Value") : null;
        var rightNull = rightNullable ? Expression.Constant(null, right.Type) : null;

        //Only left nullable
        if (leftNullable && !rightNullable)
        {
            var ternary = Expression.Condition(Expression.Not(leftHasValue), leftNull, Expression.Convert(leftValue, left.Type));
            var argument = Expression.Convert(right, left.Type);
            var binaryOperatorExpression = binaryOperatorExpressionFunc(ternary, argument);
            return Expression.And(leftHasValue, binaryOperatorExpression);
        }

        //Only right nullable
        if (rightNullable && !leftNullable)
        {
            var ternary = Expression.Condition(Expression.Not(rightHasValue), rightNull, Expression.Convert(rightValue, right.Type));
            var argument = Expression.Convert(left, right.Type);
            var binaryOperatorExpression = binaryOperatorExpressionFunc(argument, ternary);
            return Expression.And(rightHasValue, binaryOperatorExpression);
        }

        //Both are nullable
        var bothHasValueExpression = Expression.Add(leftHasValue, rightHasValue);
        var leftTernary = Expression.Condition(Expression.Not(leftHasValue), leftNull, Expression.Convert(leftValue, left.Type));
        var rightTernary = Expression.Condition(Expression.Not(rightHasValue), rightNull, Expression.Convert(rightValue, right.Type));

        var resultExpression = Expression.And(bothHasValueExpression, binaryOperatorExpressionFunc(leftTernary, rightTernary));
        return resultExpression;
    }


    public string ToExpressionString() => $"{Left.ToExpressionString()} {Expr.CreateOperator(Operator)} {Right.ToExpressionString()}";
    public override string ToString() => $"{Left} { Expr.CreateOperator(Operator)} {Right}";
}
