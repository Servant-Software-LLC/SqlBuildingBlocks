using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.Visitors;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlExpression
{
    public SqlExpression(SqlColumnRef column) => Column = column;
    public SqlExpression(SqlParameter parameter) => Parameter = parameter;
    public SqlExpression(SqlFunction function) => Function = function;
    public SqlExpression(SqlLiteralValue value) => Value = value;
    public SqlExpression(SqlBinaryExpression binExpr) => BinExpr = binExpr;

    //Logic within this class should enforce that only one of these properties is ever set.
    public SqlColumnRef? Column { get; private set; }
    public SqlParameter? Parameter { get; private set; }
    public SqlFunction? Function { get; private set; }
    public SqlLiteralValue? Value { get; private set; }
    public SqlBinaryExpression? BinExpr { get; private set; }

    public Type Type 
    { 
        get
        {
            if (Column != null)
                return Column.Type;

            if (Value != null)
                return Value.GetType();

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

    }

    public Expression GetExpression(Dictionary<SqlTable, DataRow> substituteValues, SqlTable tableDataRow, ParameterExpression param, SqlExpression companionOfBinExpr)
    {
        if (BinExpr != null)
            return BinExpr.GetExpression(substituteValues, tableDataRow, param);

        if (Value != null)
        {
            var valueExpression = Value.GetExpression(companionOfBinExpr);

            if (Value.Value == null)
                throw new Exception($"The {nameof(Value.Value)} property of {nameof(Value)} is null.  Unable to determine the type of the value.");

            var castToType = CastToType(Value.Value.GetType(), companionOfBinExpr);

            return Convert(valueExpression, Value.Value.GetType(), castToType);
        }

        //TODO: Should we create a visitor to replace SqlFunction instances or is this a consumer specific type of visitor?
        if (Function != null)
            throw new Exception($"Linq {typeof(Expression)} could not be created because there is an unresolved {typeof(SqlFunction)} within it.  Call the {nameof(Accept)} method on the {typeof(SqlExpression)} instance, providing it a your own custom class derived from {typeof(ISqlExpressionVisitor)}");

        if (Parameter != null)
            throw new Exception($"Linq {typeof(Expression)} could not be created because there is an unresolved {typeof(SqlParameter)} within it.  Call the {nameof(Accept)} method on the {typeof(SqlExpression)} instance, providing it a {typeof(ResolveParametersVisitor)} instance.");

        if (Column == null)
            throw new Exception("Operand wasn't a Column as expected.");

        if (Column.Column is not SqlColumn columnOfOperand)
            throw new Exception($"The {nameof(Column)}.{nameof(Column.Column)} property must be convertable to a {typeof(SqlColumn)}.");

        if (columnOfOperand.TableRef is null)
            throw new ArgumentNullException(nameof(columnOfOperand.TableRef), $"{nameof(columnOfOperand)}.{nameof(columnOfOperand.TableRef)} cannot be null.");

        //Look for table rows that are providing substitute values.
        if (substituteValues != null && columnOfOperand.TableRef != tableDataRow &&
            substituteValues.TryGetValue(columnOfOperand.TableRef, out DataRow? rowSubstituteValues))
        {
            //TODO: Maltby - Determine if it would be more efficient to store a mapping of columnName to index in the DataRow
            var propertyValue = rowSubstituteValues[columnOfOperand.ColumnName];
            return Expression.Constant(propertyValue);
        }

        if (columnOfOperand.TableRef != tableDataRow)
            throw new Exception($"The column {columnOfOperand} of the operand, does not have a substitute value nor is it part of the {tableDataRow} table.");

        //Is this is DataRow?
        if (param.Type == typeof(DataRow))
            return GetExpressionForDataRow(param, columnOfOperand, companionOfBinExpr);

        //Call the property on the param variable.
        return Expression.Property(param, columnOfOperand.ColumnName);
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

        if (expression.Column != null)
            Column = expression.Column;
        else if (expression.Parameter != null)
            Parameter = expression.Parameter;
        else if (expression.Function != null)
            Function = expression.Function;
        else if (expression.Value != null)
            Value = expression.Value;
        else if (expression.BinExpr != null)
            BinExpr = expression.BinExpr;        
    }

    public string ToExpressionString()
    {
        if (BinExpr != null) return BinExpr.ToExpressionString();
        if (Column != null) return Column.ToExpressionString();
        if (Parameter != null) return Parameter.ToExpressionString();
        if (Function != null) return Function.ToExpressionString();
        if (Value != null) return Value.ToExpressionString();

        throw new Exception($"Expression doesn't have value.");
    }

    public override string ToString()
    {
        if (BinExpr != null) return BinExpr.ToString();
        if (Column != null) return Column.ToString();
        if (Parameter != null) return Parameter.ToString();
        if (Function != null) return Function.ToString();
        if (Value != null) return Value.ToString();

        return "SQL definition type not set";
    }
}
