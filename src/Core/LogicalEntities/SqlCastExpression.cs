using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlCastExpression
{
    public SqlCastExpression(SqlExpression expression, SqlDataType dataType)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
    }

    public SqlExpression Expression { get; }
    public SqlDataType DataType { get; }

    public void Accept(ISqlExpressionVisitor visitor)
    {
        visitor.Visit(this);
        Expression.Accept(visitor);
    }

    public string ToExpressionString()
    {
        var dataTypeName = DataType.Name;
        if (DataType.Length.HasValue)
            dataTypeName += $"({DataType.Length})";
        else if (DataType.Precision.HasValue && DataType.Scale.HasValue)
            dataTypeName += $"({DataType.Precision},{DataType.Scale})";
        else if (DataType.Precision.HasValue)
            dataTypeName += $"({DataType.Precision})";

        return $"CAST({Expression.ToExpressionString()} AS {dataTypeName})";
    }

    public override string ToString() => ToExpressionString();
}
