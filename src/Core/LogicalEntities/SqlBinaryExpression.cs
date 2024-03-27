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
        var commonType = GetCommonType(left.Type, right.Type);
        var leftCasted = CastExpression(left, commonType);
        var rightCasted = CastExpression(right, commonType);

        var leftNullable = IsNullable(left.Type);
        var rightNullable = IsNullable(right.Type);

        if (!leftNullable && !rightNullable)
            return binaryOperatorExpressionFunc(leftCasted, rightCasted);

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
            var argument = Expression.Convert(rightCasted, left.Type);
            var binaryOperatorExpression = binaryOperatorExpressionFunc(ternary, argument);
            return Expression.And(leftHasValue, binaryOperatorExpression);
        }

        //Only right nullable
        if (rightNullable && !leftNullable)
        {
            var ternary = Expression.Condition(Expression.Not(rightHasValue), rightNull, Expression.Convert(rightValue, right.Type));
            var argument = Expression.Convert(leftCasted, right.Type);
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

    private Type GetCommonType(Type type1, Type type2)
    {
        // If both types are same, return that type
        if (type1 == type2)
            return type1;

        // If one type is assignable from the other, return the assignable type
        if (type1.IsAssignableFrom(type2))
            return type1;
        if (type2.IsAssignableFrom(type1))
            return type2;

        // If one of the types is nullable, get the underlying type and try again
        if (IsNullable(type1))
            return GetCommonType(Nullable.GetUnderlyingType(type1), type2);
        if (IsNullable(type2))
            return GetCommonType(type1, Nullable.GetUnderlyingType(type2));

        // Example of handling some numeric promotions explicitly
        Dictionary<Type, int> typePrecedence = new Dictionary<Type, int>
        {
            { typeof(byte), 1 },
            { typeof(short), 2 },
            { typeof(int), 3 },
            { typeof(long), 4 },
            { typeof(float), 5 },
            { typeof(double), 6 },
            { typeof(decimal), 7 }
        };

        if (typePrecedence.TryGetValue(type1, out int type1Precedence) &&
            typePrecedence.TryGetValue(type2, out int type2Precedence))
        {
            Type higherPrecedenceType = type1Precedence > type2Precedence ? type1 : type2;

            // You might want to handle nullable types here as well
            return higherPrecedenceType;
        }

        // If no common type found, throw an exception
        throw new Exception($"No common type found for {type1} and {type2}");
    }

    private Expression CastExpression(Expression expression, Type targetType)
    {
        // If expression type matches target type, no need for casting
        if (expression.Type == targetType)
            return expression;

        // If expression is nullable and target type is non-nullable, perform null check and cast
        if (IsNullable(expression.Type) && !IsNullable(targetType))
        {
            var hasValue = Expression.Property(expression, "HasValue");
            var value = Expression.Property(expression, "Value");
            return Expression.Condition(Expression.Not(hasValue), Expression.Constant(null, targetType), Expression.Convert(value, targetType));
        }

        // Perform standard conversion
        return Expression.Convert(expression, targetType);
    }

    private bool IsNullable(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public string ToExpressionString() => $"{Left.ToExpressionString()} {Expr.CreateOperator(Operator)} {Right.ToExpressionString()}";
    public override string ToString() => $"{Left} { Expr.CreateOperator(Operator)} {Right}";
}
