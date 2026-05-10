using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.Visitors;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlExpression
{
    public SqlExpression(SqlColumnRef column)
    {
        Column = column;
        Kind = SqlExpressionKind.Column;
        AssertInvariant();
    }

    public SqlExpression(SqlParameter parameter)
    {
        Parameter = parameter;
        Kind = SqlExpressionKind.Parameter;
        AssertInvariant();
    }

    public SqlExpression(SqlFunction function)
    {
        Function = function;
        Kind = SqlExpressionKind.Function;
        AssertInvariant();
    }

    public SqlExpression(SqlLiteralValue value)
    {
        Value = value;
        Kind = SqlExpressionKind.Value;
        AssertInvariant();
    }

    public SqlExpression(SqlBinaryExpression binExpr)
    {
        BinExpr = binExpr;
        Kind = SqlExpressionKind.BinExpr;
        AssertInvariant();
    }

    public SqlExpression(SqlBetweenExpression betweenExpr)
    {
        BetweenExpr = betweenExpr;
        Kind = SqlExpressionKind.BetweenExpr;
        AssertInvariant();
    }

    public SqlExpression(SqlCaseExpression caseExpr)
    {
        CaseExpr = caseExpr;
        Kind = SqlExpressionKind.CaseExpr;
        AssertInvariant();
    }

    public SqlExpression(SqlExistsExpression existsExpr)
    {
        ExistsExpr = existsExpr;
        Kind = SqlExpressionKind.ExistsExpr;
        AssertInvariant();
    }

    public SqlExpression(SqlScalarSubqueryExpression scalarSubqueryExpr)
    {
        ScalarSubqueryExpr = scalarSubqueryExpr;
        Kind = SqlExpressionKind.ScalarSubqueryExpr;
        AssertInvariant();
    }

    public SqlExpression(SqlInList inList)
    {
        InList = inList;
        Kind = SqlExpressionKind.InList;
        AssertInvariant();
    }

    public SqlExpression(SqlCastExpression castExpr)
    {
        CastExpr = castExpr;
        Kind = SqlExpressionKind.CastExpr;
        AssertInvariant();
    }

    public SqlExpression(SqlArrayConstructor arrayConstructor)
    {
        ArrayConstructor = arrayConstructor;
        Kind = SqlExpressionKind.ArrayConstructor;
        AssertInvariant();
    }

    public SqlExpression(SqlArraySubscript arraySubscript)
    {
        ArraySubscript = arraySubscript;
        Kind = SqlExpressionKind.ArraySubscript;
        AssertInvariant();
    }

    public SqlExpression(SqlJsonExpression jsonExpr)
    {
        JsonExpr = jsonExpr;
        Kind = SqlExpressionKind.JsonExpr;
        AssertInvariant();
    }

    /// <summary>
    /// Identifies which arm of this discriminated union is populated. Set once at construction time
    /// (and re-set inside <c>AssumeExpressionLikeness</c>); always agrees with the single non-null arm.
    /// Consumers may switch on this for explicit dispatch instead of null-checking each property.
    /// </summary>
    public SqlExpressionKind Kind { get; private set; }

    //Logic within this class enforces that exactly one of these properties is non-null at any time.
    //The active arm is reported by Kind. AssertInvariant() guards every constructor and the
    //AssumeExpressionLikeness() rewrite path. Setters are private; AssumeExpressionLikeness is
    //the only path inside the assembly that mutates these after construction.
    public SqlColumnRef? Column { get; private set; }
    public SqlParameter? Parameter { get; private set; }
    public SqlFunction? Function { get; private set; }
    public SqlLiteralValue? Value { get; private set; }
    public SqlBinaryExpression? BinExpr { get; private set; }
    public SqlBetweenExpression? BetweenExpr { get; private set; }
    public SqlCaseExpression? CaseExpr { get; private set; }
    public SqlExistsExpression? ExistsExpr { get; private set; }
    public SqlScalarSubqueryExpression? ScalarSubqueryExpr { get; private set; }
    public SqlInList? InList { get; private set; }
    public SqlCastExpression? CastExpr { get; private set; }
    public SqlArrayConstructor? ArrayConstructor { get; private set; }
    public SqlArraySubscript? ArraySubscript { get; private set; }
    public SqlJsonExpression? JsonExpr { get; private set; }

    public Type Type
    {
        get
        {
            if (Column != null)
                return Column.Type;

            if (Value != null)
                return Value.GetType();

            if (Function != null)
                return Function.ValueType ?? typeof(string);

            if (ExistsExpr != null)
                return typeof(bool);

            if (ScalarSubqueryExpr != null)
                return ScalarSubqueryExpr.ValueType ?? typeof(object);

            throw new Exception($"Engine did not expect to have to get a {nameof(Type)} for {this}.");
        }
    }

    public void Accept(ISqlExpressionVisitor visitor)
    {
        //Give leaf nodes an opportunity to change this SqlExpression's likeness.

        if (HandleLeafNode(Column, leaf => leaf?.Accept(visitor)!))
            return;

        if (HandleLeafNode(Parameter, leaf => leaf?.Accept(visitor)!))
            return;

        if (HandleLeafNode(Function, leaf => leaf?.Accept(visitor)!))
            return;

        if (HandleLeafNode(Value, leaf => leaf?.Accept(visitor)!))
            return;

        if (BinExpr != null)
        {
            BinExpr.Accept(visitor);
            return;
        }

        if (BetweenExpr != null)
        {
            BetweenExpr.Accept(visitor);
            return;
        }

        if (CaseExpr != null)
        {
            CaseExpr.Accept(visitor);
            return;
        }

        if (ExistsExpr != null)
        {
            ExistsExpr.Accept(visitor);
            return;
        }

        if (ScalarSubqueryExpr != null)
        {
            ScalarSubqueryExpr.Accept(visitor);
            return;
        }

        if (InList != null)
        {
            InList.Accept(visitor);
            return;
        }


        if (CastExpr != null)
        {
            CastExpr.Accept(visitor);
            return;
        }

        if (ArrayConstructor != null)
        {
            ArrayConstructor.Accept(visitor);
            return;
        }

        if (ArraySubscript != null)
        {
            ArraySubscript.Accept(visitor);
            return;
        }

        if (JsonExpr != null)
        {
            JsonExpr.Accept(visitor);
            return;
        }


    }

    public Expression GetExpression(Dictionary<SqlTable, DataRow> substituteValues, SqlTable tableDataRow, ParameterExpression param, SqlExpression companionOfBinExpr)
    {
        // Switch on Kind so the precedence is explicit instead of an implicit null-check ladder.
        // Cases below preserve the historical precedence and behavior of the prior null-check chain.
        switch (Kind)
        {
            case SqlExpressionKind.BinExpr:
                return BinExpr!.GetExpression(substituteValues, tableDataRow, param);

            case SqlExpressionKind.BetweenExpr:
                return BetweenExpr!.GetExpression(substituteValues, tableDataRow, param);

            case SqlExpressionKind.CaseExpr:
                return CaseExpr!.GetExpression(substituteValues, tableDataRow, param);

            case SqlExpressionKind.Value:
            {
                var valueExpression = Value!.GetExpression(companionOfBinExpr);

                if (Value.Value == null)
                    throw new Exception($"The {nameof(Value.Value)} property of {nameof(Value)} is null.  Unable to determine the type of the value.");

                var castToType = CastToType(Value.Value.GetType(), companionOfBinExpr);

                return Convert(valueExpression, Value.Value.GetType(), castToType);
            }

            case SqlExpressionKind.Function:
                return GetBuiltInFunctionExpression(Function!, substituteValues, tableDataRow, param);

            case SqlExpressionKind.Parameter:
                throw new Exception($"Linq {typeof(Expression)} could not be created because there is an unresolved {typeof(SqlParameter)} within it.  Call the {nameof(Accept)} method on the {typeof(SqlExpression)} instance, providing it a {typeof(ResolveParametersVisitor)} instance.");

            case SqlExpressionKind.ScalarSubqueryExpr:
                throw new NotSupportedException("Scalar subqueries are not supported by LINQ expression generation.");

            case SqlExpressionKind.Column:
                return GetColumnExpression(substituteValues, tableDataRow, param, companionOfBinExpr);

            default:
                throw new NotSupportedException($"Generating a LINQ expression for a {nameof(SqlExpression)} of {nameof(Kind)}={Kind} is not supported.");
        }
    }

    private Expression GetColumnExpression(Dictionary<SqlTable, DataRow> substituteValues, SqlTable tableDataRow, ParameterExpression param, SqlExpression companionOfBinExpr)
    {
        if (Column == null)
            throw new Exception("Operand wasn't a Column as expected.");

        if (Column.Column is not SqlColumn columnOfOperand)
            throw new Exception($"The {nameof(Column)}.{nameof(Column.Column)} property must be convertable to a {typeof(SqlColumn)}.");

        if (columnOfOperand.TableRef is null)
            throw new ArgumentNullException(nameof(columnOfOperand.TableRef), $"{nameof(columnOfOperand)}.{nameof(columnOfOperand.TableRef)} cannot be null.");

        //Look for table rows that are providing substitute values.
        // Issue #183: use SqlTableOccurrenceComparer so a self-join's two occurrences of the same
        // table (with different aliases) are recognized as different — SqlTable.Equals ignores the
        // alias so a self-join would otherwise treat both columns as referencing the join table.
        if (substituteValues != null && !SqlTableOccurrenceComparer.Instance.Equals(columnOfOperand.TableRef, tableDataRow) &&
            substituteValues.TryGetValue(columnOfOperand.TableRef, out DataRow? rowSubstituteValues))
        {
            //TODO: Maltby - Determine if it would be more efficient to store a mapping of columnName to index in the DataRow
            var propertyValue = rowSubstituteValues[columnOfOperand.ColumnName];

            // Issue #186: A DBNull value here would be typed as DBNull at expression-build time,
            // and a downstream binary comparison against an int (or other typed) operand would
            // fail in GetCommonType ("No common type found"). Promote the value to Nullable<T> of
            // the column's declared type so the existing nullable-aware GetBinaryExpression path
            // produces the correct SQL three-valued logic result (UNKNOWN/false) for the predicate.
            var declaredType = columnOfOperand.ColumnType;
            if (declaredType != null && declaredType.IsValueType &&
                !(declaredType.IsGenericType && declaredType.GetGenericTypeDefinition() == typeof(Nullable<>)))
            {
                var nullableType = typeof(Nullable<>).MakeGenericType(declaredType);
                if (propertyValue == null || propertyValue == DBNull.Value)
                    return Expression.Constant(null, nullableType);

                // Box the typed value into Nullable<T> so the operand types align with the
                // DataRow-backed side, which is also produced as Nullable<T> below.
                return Expression.Constant(System.Activator.CreateInstance(nullableType, propertyValue), nullableType);
            }

            // Reference type or unknown declared type: a DBNull would still compare incorrectly,
            // so substitute null when DBNull is observed at build time.
            if (propertyValue == DBNull.Value)
                return Expression.Constant(null, declaredType ?? typeof(object));

            return Expression.Constant(propertyValue);
        }

        // Issue #183: alias-aware "is this column referencing the table being filtered?" check.
        if (!SqlTableOccurrenceComparer.Instance.Equals(columnOfOperand.TableRef, tableDataRow))
            throw new Exception($"The column {columnOfOperand} of the operand, does not have a substitute value nor is it part of the {tableDataRow} table.");

        //Is this is DataRow?
        if (param.Type == typeof(DataRow))
            return GetExpressionForDataRow(param, columnOfOperand, companionOfBinExpr);

        //Call the property on the param variable.
        return Expression.Property(param, columnOfOperand.ColumnName);
    }

    private Expression GetBuiltInFunctionExpression(SqlFunction function, Dictionary<SqlTable, DataRow> substituteValues, SqlTable tableDataRow, ParameterExpression param)
    {
        var funcName = function.FunctionName.ToUpperInvariant();

        // Get the argument expression (the raw value from the data source)
        Expression argExpr = GetFunctionArgumentExpression(function.Arguments[0], substituteValues, tableDataRow, param);

        // Convert to string for string functions
        var toStringMethod = typeof(object).GetMethod("ToString", Type.EmptyTypes)!;
        var strExpr = argExpr.Type == typeof(string) ? argExpr : Expression.Call(argExpr, toStringMethod);

        switch (funcName)
        {
            case "UPPER":
                return Expression.Call(strExpr, typeof(string).GetMethod("ToUpper", Type.EmptyTypes)!);
            case "LOWER":
                return Expression.Call(strExpr, typeof(string).GetMethod("ToLower", Type.EmptyTypes)!);
            case "LENGTH":
            case "LEN":
                return Expression.Property(strExpr, "Length");
            case "ABS":
                var absArgExpr = argExpr.Type == typeof(object) ? Expression.Convert(argExpr, typeof(double)) : argExpr;
                return Expression.Call(typeof(Math), "Abs", null, Expression.Convert(absArgExpr, typeof(double)));
            case "ROUND":
                var roundArgExpr = argExpr.Type == typeof(object) ? Expression.Convert(argExpr, typeof(double)) : argExpr;
                int digits = 0;
                if (function.Arguments.Count > 1)
                {
                    var digitsLiteral = function.Arguments[1].Value;
                    if (digitsLiteral != null)
                        digits = System.Convert.ToInt32(digitsLiteral.Value);
                }
                return Expression.Call(typeof(Math), "Round", null,
                    Expression.Convert(roundArgExpr, typeof(double)),
                    Expression.Constant(digits));
            default:
                throw new Exception($"Linq {typeof(Expression)} could not be created because there is an unresolved {typeof(SqlFunction)} '{function.FunctionName}' within it.  Call the {nameof(Accept)} method on the {typeof(SqlExpression)} instance, providing it a your own custom class derived from {typeof(ISqlExpressionVisitor)}");
        }
    }

    private Expression GetFunctionArgumentExpression(SqlExpression arg, Dictionary<SqlTable, DataRow> substituteValues, SqlTable tableDataRow, ParameterExpression param)
    {
        if (arg.Value != null)
            return Expression.Constant(arg.Value.Value, arg.Value.Value?.GetType() ?? typeof(object));

        if (arg.Function != null)
            return GetBuiltInFunctionExpression(arg.Function, substituteValues, tableDataRow, param);

        if (arg.Column?.Column is SqlColumn argCol)
        {
            if (param.Type == typeof(DataRow))
            {
                var columnNameExpr = Expression.Constant(argCol.ColumnName);
                var indexerProperty = typeof(DataRow).GetProperty("Item", new Type[] { typeof(string) });
                return Expression.MakeIndex(param, indexerProperty!, new[] { columnNameExpr });
            }

            return Expression.Property(param, argCol.ColumnName);
        }

        throw new NotSupportedException("Function argument must be a column reference or literal value.");
    }

    private Expression GetExpressionForDataRow(ParameterExpression param, SqlColumn? columnOfOperand, SqlExpression companionOfBinExpr)
    {
        if (columnOfOperand is null)
            throw new ArgumentNullException(nameof(columnOfOperand), $"{nameof(columnOfOperand)} cannot be null.");

        //Since this is a DataRow, call param[columnOfOperand.ColumnName] to get the value of the column in the DataRow.

        // Create a constant expression for the column name
        var columnNameExpression = Expression.Constant(columnOfOperand.ColumnName);

        // Get the indexer property information
        var indexerProperty = typeof(DataRow).GetProperty("Item", new Type[] { typeof(string) });

        var valueExpression = Expression.MakeIndex(param, indexerProperty, new[] { columnNameExpression });

        if (columnOfOperand.ColumnType is null)
            throw new Exception($"Expected the {columnOfOperand} column to have its {nameof(SqlColumn.ColumnType)} property set.");

        var castToType = CastToType(columnOfOperand.ColumnType, companionOfBinExpr);

        // Issue #186: a DBNull value in the underlying DataRow column cannot be unboxed/converted
        // to the declared value type at runtime (Expression.Convert(DBNull, int) throws). Wrap the
        // raw indexer value in a runtime DBNull check that produces Nullable<T> for value types
        // so the existing nullable-aware GetBinaryExpression path enforces SQL three-valued logic
        // (any comparison with a NULL operand other than IS NULL / IS NOT NULL → UNKNOWN/false).
        // IS NULL / IS NOT NULL go through GetIsNullExpression which reads the raw indexer directly.
        if (castToType.IsValueType &&
            !(castToType.IsGenericType && castToType.GetGenericTypeDefinition() == typeof(Nullable<>)))
        {
            var nullableType = typeof(Nullable<>).MakeGenericType(castToType);
            var isDbNull = Expression.Equal(valueExpression, Expression.Constant(DBNull.Value, typeof(object)));
            var nullValue = Expression.Constant(null, nullableType);
            var convertedValue = Expression.Convert(Expression.Convert(valueExpression, castToType), nullableType);
            return Expression.Condition(isDbNull, nullValue, convertedValue);
        }

        return Convert(valueExpression, columnOfOperand.ColumnType, castToType);
    }

    private Expression Convert(Expression expression, Type fromType, Type toType)
    {
        if (fromType == toType && expression.Type == fromType)
            return expression;

        if (toType != typeof(string) || (expression.Type == typeof(object) && fromType == typeof(string)))
            return Expression.Convert(expression, toType);

        MethodInfo toStringMethod = expression.Type.GetMethod("ToString", Type.EmptyTypes);
        return Expression.Call(expression, toStringMethod);
    }

    private Type CastToType(Type thisType, SqlExpression companionOfBinExpr)
    {
        var companionType = companionOfBinExpr.Type;

        if (thisType == typeof(int))
        {
            if (companionType == typeof(string))
                return typeof(string);

            if (companionType == typeof(decimal))
                return typeof(decimal);
        }

        if (thisType == typeof(decimal))
        {
            if (companionType == typeof(string))
                return typeof(string);
        }

        return thisType;
    }

    private bool HandleLeafNode<TLeafNode>(TLeafNode current, Func<TLeafNode, SqlExpression> accept)
    {
        if (current != null)
        {
            var potentiallyNewExpr = accept(current);

            // If the returned expression is the same, then don't morph our likeness.
            if (potentiallyNewExpr != null && !ReferenceEquals(current, potentiallyNewExpr))
                AssumeExpressionLikeness(potentiallyNewExpr);

            return true;
        }

        return false;
    }

    private void AssumeExpressionLikeness(SqlExpression expression)
    {
        Column = null;
        Parameter = null;
        Function = null;
        Value = null;
        BinExpr = null;
        BetweenExpr = null;
        CaseExpr = null;
        ExistsExpr = null;
        ScalarSubqueryExpr = null;
        InList = null;
        CastExpr = null;
        ArrayConstructor = null;
        ArraySubscript = null;
        JsonExpr = null;

        // Copy the single populated arm AND mirror Kind from the source expression so the invariant
        // holds at exit. The source has already been validated by its own constructor (or a prior
        // AssumeExpressionLikeness call), so its Kind is the authoritative discriminator here.
        if (expression.Column != null)
        {
            Column = expression.Column;
            Kind = SqlExpressionKind.Column;
        }
        else if (expression.Parameter != null)
        {
            Parameter = expression.Parameter;
            Kind = SqlExpressionKind.Parameter;
        }
        else if (expression.Function != null)
        {
            Function = expression.Function;
            Kind = SqlExpressionKind.Function;
        }
        else if (expression.Value != null)
        {
            Value = expression.Value;
            Kind = SqlExpressionKind.Value;
        }
        else if (expression.BinExpr != null)
        {
            BinExpr = expression.BinExpr;
            Kind = SqlExpressionKind.BinExpr;
        }
        else if (expression.BetweenExpr != null)
        {
            BetweenExpr = expression.BetweenExpr;
            Kind = SqlExpressionKind.BetweenExpr;
        }
        else if (expression.CaseExpr != null)
        {
            CaseExpr = expression.CaseExpr;
            Kind = SqlExpressionKind.CaseExpr;
        }
        else if (expression.ExistsExpr != null)
        {
            ExistsExpr = expression.ExistsExpr;
            Kind = SqlExpressionKind.ExistsExpr;
        }
        else if (expression.ScalarSubqueryExpr != null)
        {
            ScalarSubqueryExpr = expression.ScalarSubqueryExpr;
            Kind = SqlExpressionKind.ScalarSubqueryExpr;
        }
        else if (expression.InList != null)
        {
            InList = expression.InList;
            Kind = SqlExpressionKind.InList;
        }
        else if (expression.CastExpr != null)
        {
            CastExpr = expression.CastExpr;
            Kind = SqlExpressionKind.CastExpr;
        }
        else if (expression.ArrayConstructor != null)
        {
            ArrayConstructor = expression.ArrayConstructor;
            Kind = SqlExpressionKind.ArrayConstructor;
        }
        else if (expression.ArraySubscript != null)
        {
            ArraySubscript = expression.ArraySubscript;
            Kind = SqlExpressionKind.ArraySubscript;
        }
        else if (expression.JsonExpr != null)
        {
            JsonExpr = expression.JsonExpr;
            Kind = SqlExpressionKind.JsonExpr;
        }
        else
        {
            throw new InvalidOperationException(
                $"{nameof(AssumeExpressionLikeness)} received a {nameof(SqlExpression)} with no populated arm; cannot adopt its likeness.");
        }

        AssertInvariant();
    }

    /// <summary>
    /// Validates the discriminated-union invariant: exactly one arm is non-null AND that arm
    /// matches <see cref="Kind"/>. Called at the end of every public constructor and at the end of
    /// <see cref="AssumeExpressionLikeness"/>. Throws <see cref="InvalidOperationException"/>
    /// with a diagnostic message naming the violation if anything is off.
    /// </summary>
    private void AssertInvariant()
    {
        // Build a set of the arms that are currently non-null. If the count is anything other
        // than 1, the union shape is broken. If it is 1, that single arm must match Kind.
        var populated = new List<SqlExpressionKind>();
        if (Column != null) populated.Add(SqlExpressionKind.Column);
        if (Parameter != null) populated.Add(SqlExpressionKind.Parameter);
        if (Function != null) populated.Add(SqlExpressionKind.Function);
        if (Value != null) populated.Add(SqlExpressionKind.Value);
        if (BinExpr != null) populated.Add(SqlExpressionKind.BinExpr);
        if (BetweenExpr != null) populated.Add(SqlExpressionKind.BetweenExpr);
        if (CaseExpr != null) populated.Add(SqlExpressionKind.CaseExpr);
        if (ExistsExpr != null) populated.Add(SqlExpressionKind.ExistsExpr);
        if (ScalarSubqueryExpr != null) populated.Add(SqlExpressionKind.ScalarSubqueryExpr);
        if (InList != null) populated.Add(SqlExpressionKind.InList);
        if (CastExpr != null) populated.Add(SqlExpressionKind.CastExpr);
        if (ArrayConstructor != null) populated.Add(SqlExpressionKind.ArrayConstructor);
        if (ArraySubscript != null) populated.Add(SqlExpressionKind.ArraySubscript);
        if (JsonExpr != null) populated.Add(SqlExpressionKind.JsonExpr);

        if (populated.Count == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(SqlExpression)} invariant violated: no arm is populated. Expected exactly one arm matching {nameof(Kind)}={Kind}.");
        }

        if (populated.Count > 1)
        {
            throw new InvalidOperationException(
                $"{nameof(SqlExpression)} invariant violated: {populated.Count} arms are populated ({string.Join(", ", populated)}). Exactly one arm must be non-null.");
        }

        if (populated[0] != Kind)
        {
            throw new InvalidOperationException(
                $"{nameof(SqlExpression)} invariant violated: populated arm is {populated[0]} but {nameof(Kind)} is {Kind}. They must agree.");
        }
    }

    public string ToExpressionString()
    {
        if (BinExpr != null) return BinExpr.ToExpressionString();
        if (BetweenExpr != null) return BetweenExpr.ToExpressionString();
        if (CaseExpr != null) return CaseExpr.ToExpressionString();
        if (ExistsExpr != null) return ExistsExpr.ToExpressionString();
        if (ScalarSubqueryExpr != null) return ScalarSubqueryExpr.ToExpressionString();
        if (InList != null) return InList.ToExpressionString();
        if (CastExpr != null) return CastExpr.ToExpressionString();
        if (ArrayConstructor != null) return ArrayConstructor.ToExpressionString();
        if (ArraySubscript != null) return ArraySubscript.ToExpressionString();
        if (JsonExpr != null) return JsonExpr.ToExpressionString();
        if (Column != null) return Column.ToExpressionString();
        if (Parameter != null) return Parameter.ToExpressionString();
        if (Function != null) return Function.ToExpressionString();
        if (Value != null) return Value.ToExpressionString();

        throw new Exception($"Expression doesn't have value.");
    }

    public override string ToString()
    {
        if (BinExpr != null) return BinExpr.ToString();
        if (BetweenExpr != null) return BetweenExpr.ToString();
        if (CaseExpr != null) return CaseExpr.ToString();
        if (ExistsExpr != null) return ExistsExpr.ToString();
        if (ScalarSubqueryExpr != null) return ScalarSubqueryExpr.ToString();
        if (InList != null) return InList.ToString();
        if (CastExpr != null) return CastExpr.ToString();
        if (ArrayConstructor != null) return ArrayConstructor.ToString();
        if (ArraySubscript != null) return ArraySubscript.ToString();
        if (JsonExpr != null) return JsonExpr.ToString();
        if (Column != null) return Column.ToString();
        if (Parameter != null) return Parameter.ToString();
        if (Function != null) return Function.ToString();
        if (Value != null) return Value.ToString();

        return "SQL definition type not set";
    }
}
