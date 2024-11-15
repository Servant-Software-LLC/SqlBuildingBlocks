using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlParameter : IEquatable<SqlParameter>
{
    public const string PositionalChar = "?";

    public SqlParameter(string? name = null)
    {
        Type = string.IsNullOrEmpty(name) || name == PositionalChar ? ParameterType.Positional : ParameterType.Named;
        Name = name;
    }   

    public enum ParameterType { Positional, Named }

    public ParameterType Type { get; set; }

    public string? Name { get; set; }

    public SqlExpression? Accept(ISqlExpressionVisitor sqlExpressionVisitor) => sqlExpressionVisitor.Visit(this);

    public SqlLimitValue? Accept(ISqlValueVisitor sqlValueVisitor)
    {
        var sqlLiteralValue = sqlValueVisitor.Visit(this);
        if (sqlLiteralValue == null)
            return null;

        if (sqlLiteralValue.Int.HasValue)
            return new(sqlLiteralValue.Int.Value);

        if (sqlLiteralValue.Float.HasValue)
            return new((int)sqlLiteralValue.Float.Value);

        if (sqlLiteralValue.Double.HasValue)
            return new((int)sqlLiteralValue.Double.Value);

        if (sqlLiteralValue.Decimal.HasValue)
            return new((int)sqlLiteralValue.Decimal.Value);

        var literalValueType = sqlLiteralValue.DBNull ? "NULL" : $"{sqlLiteralValue.Value}({sqlLiteralValue.Value!.GetType().Name})";
        throw new NotSupportedException($"The replacement of the {nameof(SqlParameter)} with a literal value encountered an unexpected type of {literalValueType}");
    }

    public string ToExpressionString() => ToString();

    public override string ToString() => Type == ParameterType.Named ? $"@{Name}" : PositionalChar;

    public override bool Equals(object obj)
    {
        if (obj is SqlParameter sqlParameter)
        {
            return Equals(sqlParameter);
        }

        return false;
    }

    public bool Equals(SqlParameter other)
    {
        // If parameter is null, return false.
        if (ReferenceEquals(other, null)) return false;

        // Optimization for a common success case.
        if (ReferenceEquals(this, other)) return true;

        // If run-time types are not exactly the same, return false.
        if (GetType() != other.GetType()) return false;

        // Check whether the products' properties are equal.
        return (Type == other.Type) && (Type != ParameterType.Named || Name == other.Name);
    }

    // Override GetHashCode
    public override int GetHashCode()
    {
        // Use prime numbers to calculate hash code
        int hash = 17;
        hash = (hash * 23) + Type.GetHashCode();
        if (Type == ParameterType.Named)
            hash = (hash * 23) + Name!.GetHashCode();
        return hash;
    }
}
