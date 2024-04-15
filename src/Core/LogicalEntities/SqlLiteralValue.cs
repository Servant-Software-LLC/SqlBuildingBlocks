using SqlBuildingBlocks.Interfaces;
using System.Linq.Expressions;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlLiteralValue
{
    public SqlLiteralValue() { }
    public SqlLiteralValue(string str) => String = str;
    public SqlLiteralValue(int integer) => Int = integer;
    public SqlLiteralValue(decimal dec) => Decimal = dec;
    public SqlLiteralValue(bool boolean) => Boolean = boolean;
    public SqlLiteralValue(object? value)
    {
        if (value == null) 
            return;

        if (value is string str)
        {
            String = str;
            return;
        }

        if (value is int integer)
        {
            Int = integer; 
            return;
        }

        if (value is decimal dec)
        {
            Decimal = dec;
            return;
        }

        if (value is bool boolean)
        {
            Boolean = boolean;
            return;
        }

        if (value == System.DBNull.Value)
        {
            DBNull = true;
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(value), $"Object provided to the constructor of { nameof(SqlLiteralValue)} is not a supported type.  Type = {value.GetType()}");
    }

    public string? String { get; }
    public int? Int { get; }
    public decimal? Decimal { get; }
    public bool? Boolean { get; }
    public bool DBNull { get; }

    public object? Value
    {
        get
        {
            if (String != null)
                return String;

            if (Int != null)
                return Int;

            if (Decimal != null)
                return Decimal;

            if (Boolean != null)
                return Boolean;

            if (DBNull) 
                return System.DBNull.Value;

            return null;
        }
    }
                            
    public SqlExpression? Accept(ISqlExpressionVisitor visitor) => visitor.Visit(this);

    public Expression GetExpression(SqlExpression companionOfBinExpr)
    {
        if (String != null)
            return Expression.Constant(String);

        if (Int != null)
        {
            if (companionOfBinExpr.Column != null)
            {
                if (companionOfBinExpr.Column.Column is not SqlColumn columnOfCompanionOperand)
                    throw new Exception($"Expected the companionOfBinExpr.Column.Column column to be a {nameof(SqlColumn)}.");

                if (columnOfCompanionOperand.ColumnType == null)
                    throw new Exception($"Expected the {columnOfCompanionOperand} column to have its {nameof(SqlColumn.ColumnType)} property set.");

                if (columnOfCompanionOperand.ColumnType == typeof(long))
                {
                    //Upgrade our literal to be an int64.                    
                    return Expression.Constant((long)Int);
                }

            }

            return Expression.Constant(Int);
        }

        if (Decimal != null)
        {
            return Expression.Constant(Decimal);
        }

        if (Boolean != null)
        {
            return Expression.Constant(Boolean);
        }

        return Expression.Constant(null);
    }

    public string ToExpressionString() => ToString();

    public override string ToString() =>
        String != null ? $"'{String}'" :
        Int != null ? Int.ToString() :
        Decimal != null ? Decimal.ToString() :
        Boolean != null ? Boolean.ToString().ToUpperInvariant() :
        DBNull ? "DBNull" :
        "NULL";
}
