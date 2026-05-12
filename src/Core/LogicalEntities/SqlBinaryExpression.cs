using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.Utils;
using System.Data;
using System.Linq.Expressions;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlBinaryExpression
{
    public SqlBinaryExpression(SqlExpression left, SqlBinaryOperator binaryOperator, SqlExpression? right)
    {
        Left = left;
        Operator = binaryOperator;
        Right = right;
    }

    public SqlExpression Left { get; set; }
    public SqlBinaryOperator Operator { get; set; }
    public SqlExpression? Right { get; set; }

    public void Accept(ISqlExpressionVisitor visitor)
    {
        visitor.Visit(this);
        Left.Accept(visitor);
        Right?.Accept(visitor);
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

    /// <summary>
    /// Returns a cached compiled predicate that closes over the given <paramref name="tableDataRow"/>
    /// (the SqlTable being filtered on) and accepts the substitute-values dictionary at runtime.
    ///
    /// Issue #188: <see cref="BuildExpression{TDataRow}"/> baked substitute-value constants into a
    /// fresh <see cref="Expression{TDelegate}"/> per call and re-compiled every time. For a 100-row
    /// JOIN that meant 100 compiles for the same structural predicate. This method amortizes the
    /// compile across all calls with the same predicate shape + <typeparamref name="TDataRow"/> +
    /// <paramref name="tableDataRow"/> tuple by computing a structural hash, looking up
    /// <see cref="CompiledPredicateCache"/>, and on miss building a lambda that emits
    /// dictionary-lookup IL for substitute reads (instead of constants), then compiling once.
    ///
    /// The returned delegate is invoked as <c>predicate(row, substituteValues)</c> where the
    /// dictionary may be <c>null</c> (e.g., a top-level WHERE with no JOIN context).
    ///
    /// Predicates whose tree contains arms not handled by the cached path (BETWEEN, CASE, IN, etc.)
    /// would silently bake constants from a <c>null</c> substituteValues. Callers must check
    /// <see cref="IsCacheableShape"/> first and fall back to <see cref="BuildExpression{TDataRow}"/>
    /// when it returns false. This keeps the cache implementation surgical — it does not need to
    /// thread the runtime-substitute-parameter through every <see cref="SqlExpression"/> arm.
    /// </summary>
    public Func<TDataRow, Dictionary<SqlTable, DataRow>?, bool> BuildCompiledPredicate<TDataRow>(SqlTable tableDataRow)
    {
        return CompiledPredicateCache.GetOrAdd<TDataRow>(this, tableDataRow, () =>
        {
            var rowParam = Expression.Parameter(typeof(TDataRow), "dataRow");
            var substituteParam = Expression.Parameter(typeof(Dictionary<SqlTable, DataRow>), "subst");

            // GetExpression with a non-null substituteValuesParam emits dictionary-lookup IL for
            // substitute-table column reads instead of baking literal constants. The other arms
            // (literal values, function calls, column reads on the local row) are unchanged.
            var body = GetExpression(substituteValues: null, tableDataRow, rowParam, substituteParam);

            var lambda = Expression.Lambda<Func<TDataRow, Dictionary<SqlTable, DataRow>?, bool>>(body, rowParam, substituteParam);
            return lambda.Compile();
        });
    }

    /// <summary>
    /// Issue #188: returns true when this binary-expression tree consists solely of arms whose
    /// <c>GetExpression</c> emission paths thread <paramref name="substituteValuesParam"/> through
    /// to <see cref="SqlExpression.GetColumnExpression"/>. False for any tree containing BETWEEN,
    /// CASE, IN, NOT IN, function calls, or other arms whose <c>GetExpression</c> overloads in
    /// other logical-entity files do not yet accept the runtime-substitute parameter — those must
    /// fall back to the legacy per-call <see cref="BuildExpression{TDataRow}"/> compile path.
    ///
    /// The conservative default for any unrecognized arm is "not cacheable", which means new arm
    /// types automatically use the slow path until they are explicitly opted into the cache.
    ///
    /// Issue #225: when <paramref name="tableDataRow"/> is non-null, column arms that reference a
    /// different table (cross-table references, e.g. JOIN ON conditions) are treated as
    /// non-cacheable because <see cref="BuildCompiledPredicate{TDataRow}"/> wraps substitute values
    /// in <c>Nullable&lt;T&gt;</c> but <see cref="CastExpression"/> cannot create
    /// <c>Expression.Constant(null, typeof(int))</c> for non-nullable value types. Falling back to
    /// the legacy <see cref="BuildExpression{TDataRow}"/> path for cross-table predicates avoids
    /// the <see cref="ArgumentException"/> that propagated as 0-row or exception results in JOIN ON
    /// conditions.
    /// </summary>
    public bool IsCacheableShape(SqlTable? tableDataRow = null)
    {
        // IS NULL / IS NOT NULL: only the Left is consulted, and only via column-or-DataRow access
        // (the GetIsNullExpression DataRow short-circuit is value-and-substitute-independent).
        if (Operator == SqlBinaryOperator.IsNull || Operator == SqlBinaryOperator.IsNotNull)
            return IsCacheableArm(Left, tableDataRow);

        // IN / NOT IN: GetInExpression iterates through Right.InList without threading the
        // substitute parameter — disable caching to avoid silently dropping substitute reads.
        if (Operator == SqlBinaryOperator.In || Operator == SqlBinaryOperator.NotIn)
            return false;

        // Standard binary: both arms must be cacheable.
        return IsCacheableArm(Left, tableDataRow) && (Right == null || IsCacheableArm(Right, tableDataRow));
    }

    private static bool IsCacheableArm(SqlExpression expression, SqlTable? tableDataRow)
    {
        switch (expression.Kind)
        {
            case SqlExpressionKind.Column:
                // Issue #225: a column arm whose TableRef resolves to a different table than
                // tableDataRow is a cross-table reference (e.g. the right-hand side of a JOIN ON
                // condition). BuildCompiledPredicate wraps substitute DataRow values in Nullable<T>
                // for value-type columns, but CastExpression then tries to create
                // Expression.Constant(null, typeof(int)) for a non-nullable int target — which
                // throws ArgumentException. Returning false here forces the predicate to the
                // legacy BuildExpression path that bakes the actual value as Expression.Constant.
                if (tableDataRow is not null)
                {
                    var sqlColumn = expression.Column!.Column as SqlColumn;
                    if (sqlColumn?.TableRef is SqlTable columnTableRef &&
                        !SqlTableOccurrenceComparer.Instance.Equals(columnTableRef, tableDataRow))
                        return false;
                }
                return true;

            case SqlExpressionKind.Value:
                return true;

            case SqlExpressionKind.BinExpr:
                return expression.BinExpr!.IsCacheableShape(tableDataRow);

            // BetweenExpr, CaseExpr, Function, InList, Parameter, ExistsExpr, ScalarSubqueryExpr,
            // CastExpr, ArrayConstructor, ArraySubscript, JsonExpr — none of these flow the
            // substituteValuesParam through their GetExpression overloads in other files, so a
            // cached compile would silently bake null-dictionary constants. Stay on the legacy path.
            default:
                return false;
        }
    }

    public Expression GetExpression(Dictionary<SqlTable, DataRow>? substituteValues, SqlTable tableDataRow, ParameterExpression param)
        => GetExpression(substituteValues, tableDataRow, param, substituteValuesParam: null);

    /// <summary>
    /// Issue #188: when <paramref name="substituteValuesParam"/> is non-null, substitute-table
    /// column reads emit runtime dictionary-lookup IL (cached-compile path). When it is null, the
    /// historical behavior bakes constants from <paramref name="substituteValues"/>.
    /// </summary>
    internal Expression GetExpression(Dictionary<SqlTable, DataRow>? substituteValues, SqlTable tableDataRow, ParameterExpression param, ParameterExpression? substituteValuesParam)
    {
        // IS NULL and IS NOT NULL are unary postfix predicates — no right operand needed.
        if (Operator == SqlBinaryOperator.IsNull || Operator == SqlBinaryOperator.IsNotNull)
            return GetIsNullExpression(substituteValues, tableDataRow, param, substituteValuesParam);

        // IN / NOT IN: the right-hand side is an InList, not a simple expression.
        if (Operator == SqlBinaryOperator.In)
            return GetInExpression(substituteValues, tableDataRow, param, substituteValuesParam);
        if (Operator == SqlBinaryOperator.NotIn)
            return Expression.Not(GetInExpression(substituteValues, tableDataRow, param, substituteValuesParam));

        var leftProperty = Left.GetExpression(substituteValues, tableDataRow, param, Right!, substituteValuesParam);
        var rightProperty = Right!.GetExpression(substituteValues, tableDataRow, param, Left, substituteValuesParam);

        return Operator switch
        {
            SqlBinaryOperator.Equal => GetBinaryExpression(leftProperty, rightProperty, Expression.Equal),
            SqlBinaryOperator.NotEqualTo => GetBinaryExpression(leftProperty, rightProperty, Expression.NotEqual),
            SqlBinaryOperator.LessThan => GetBinaryExpression(leftProperty, rightProperty, Expression.LessThan),
            SqlBinaryOperator.LessThanEqual => GetBinaryExpression(leftProperty, rightProperty, Expression.LessThanOrEqual),
            SqlBinaryOperator.GreaterThan => GetBinaryExpression(leftProperty, rightProperty, Expression.GreaterThan),
            SqlBinaryOperator.GreaterThanEqual => GetBinaryExpression(leftProperty, rightProperty, Expression.GreaterThanOrEqual),
            SqlBinaryOperator.And => Expression.And(leftProperty, rightProperty),
            SqlBinaryOperator.Or => Expression.Or(leftProperty, rightProperty),
            SqlBinaryOperator.Like => GetRegexIsMatchExpression(leftProperty, rightProperty),
            SqlBinaryOperator.NotLike => Expression.Not(GetRegexIsMatchExpression(leftProperty, rightProperty)),
            _ => throw new ArgumentException($"Invalid binary operator {Operator} in {nameof(GetExpression)}", nameof(Operator))
        };
    }

    private Expression GetInExpression(Dictionary<SqlTable, DataRow>? substituteValues, SqlTable tableDataRow, ParameterExpression param, ParameterExpression? substituteValuesParam)
    {
        if (Right?.InList == null)
            throw new InvalidOperationException("IN / NOT IN requires an InList on the right-hand side.");

        // Build the left-hand expression using a dummy companion for type inference
        var dummyCompanion = Right.InList.Items.Count > 0 ? Right.InList.Items[0] : new SqlExpression(new SqlLiteralValue());
        var leftExpr = Left.GetExpression(substituteValues, tableDataRow, param, dummyCompanion, substituteValuesParam);

        // Build an OR chain: left == item1 || left == item2 || ...
        Expression? result = null;
        foreach (var item in Right.InList.Items)
        {
            var itemExpr = item.GetExpression(substituteValues, tableDataRow, param, Left, substituteValuesParam);

            // Align types for comparison
            Expression leftAligned = leftExpr;
            Expression itemAligned = itemExpr;

            if (leftExpr.Type != itemExpr.Type)
            {
                if (leftExpr.Type == typeof(object))
                    leftAligned = Expression.Convert(leftExpr, itemExpr.Type);
                else if (itemExpr.Type == typeof(object))
                    itemAligned = Expression.Convert(itemExpr, leftExpr.Type);
                else
                {
                    // Convert both to a common type
                    try
                    {
                        itemAligned = Expression.Convert(itemExpr, leftExpr.Type);
                    }
                    catch
                    {
                        leftAligned = Expression.Convert(leftExpr, itemExpr.Type);
                    }
                }
            }

            var equalExpr = Expression.Equal(leftAligned, itemAligned);
            result = result == null ? (Expression)equalExpr : Expression.OrElse(result, equalExpr);
        }

        return result ?? Expression.Constant(false);
    }

    private Expression GetIsNullExpression(Dictionary<SqlTable, DataRow>? substituteValues, SqlTable tableDataRow, ParameterExpression param, ParameterExpression? substituteValuesParam)
    {
        // For DataRow parameters, check for DBNull.Value directly on the raw object
        // without type-converting the value (which would throw on DBNull).
        if (param.Type == typeof(DataRow) && Left.Column != null)
        {
            var columnNameExpr = Expression.Constant(Left.Column.ColumnName);
            var indexerProperty = typeof(DataRow).GetProperty("Item", new Type[] { typeof(string) });
            var rawValue = Expression.MakeIndex(param, indexerProperty, new[] { columnNameExpr });
            var isNullCheck = Expression.Equal(rawValue, Expression.Constant(DBNull.Value, typeof(object)));
            return Operator == SqlBinaryOperator.IsNull ? isNullCheck : Expression.Not(isNullCheck);
        }

        // Use a dummy companion expression (null literal) to satisfy the GetExpression signature
        var nullCompanion = new SqlExpression(new SqlLiteralValue());
        var leftProperty = Left.GetExpression(substituteValues, tableDataRow, param, nullCompanion, substituteValuesParam);

        Expression isNullCheckExpr;
        if (leftProperty.Type.IsValueType && !(leftProperty.Type.IsGenericType && leftProperty.Type.GetGenericTypeDefinition() == typeof(Nullable<>)))
        {
            // Non-nullable value type: IS NULL is always false, IS NOT NULL is always true
            isNullCheckExpr = Expression.Constant(false);
        }
        else if (leftProperty.Type.IsGenericType && leftProperty.Type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            // Nullable value type: check HasValue
            isNullCheckExpr = Expression.Not(Expression.Property(leftProperty, "HasValue"));
        }
        else
        {
            // Reference type or object: compare to null
            isNullCheckExpr = Expression.Equal(leftProperty, Expression.Constant(null, leftProperty.Type));
        }

        return Operator == SqlBinaryOperator.IsNull ? isNullCheckExpr : Expression.Not(isNullCheckExpr);
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

        //Both are nullable.
        //Issue #186: previous code used Expression.Add on two bool HasValue properties — that
        //throws "the binary operator Add is not defined for the types 'System.Boolean' and 'System.Boolean'."
        //The intent is bothHasValue AND comparisonOnValues. AndAlso short-circuits so the
        //comparison is only evaluated when both sides have a value, mirroring SQL three-valued
        //logic — any comparison (other than IS NULL / IS NOT NULL) involving NULL → UNKNOWN, here
        //modeled as false so the row is excluded.
        var bothHasValueExpression = Expression.AndAlso(leftHasValue, rightHasValue);

        // Promote both sides to a common non-nullable type so the underlying binary operator
        // (Equal, LessThan, etc.) returns a plain bool — Expression.AndAlso requires bool/bool.
        var leftUnwrappedType = Nullable.GetUnderlyingType(left.Type)!;
        var rightUnwrappedType = Nullable.GetUnderlyingType(right.Type)!;
        var commonValueType = GetCommonType(leftUnwrappedType, rightUnwrappedType);
        var leftValueCasted = Expression.Convert(leftValue, commonValueType);
        var rightValueCasted = Expression.Convert(rightValue, commonValueType);

        var resultExpression = Expression.AndAlso(bothHasValueExpression, binaryOperatorExpressionFunc(leftValueCasted, rightValueCasted));
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

    public string ToExpressionString() =>
        Operator == SqlBinaryOperator.IsNull || Operator == SqlBinaryOperator.IsNotNull
            ? $"{Left.ToExpressionString()} {Expr.CreateOperator(Operator)}"
            : $"{Left.ToExpressionString()} {Expr.CreateOperator(Operator)} {Right!.ToExpressionString()}";

    public override string ToString() =>
        Operator == SqlBinaryOperator.IsNull || Operator == SqlBinaryOperator.IsNotNull
            ? $"{Left} {Expr.CreateOperator(Operator)}"
            : $"{Left} {Expr.CreateOperator(Operator)} {Right}";
}
