using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents a PostgreSQL ARRAY constructor expression, e.g. <c>ARRAY[1, 2, 3]</c>.
/// </summary>
public class SqlArrayConstructor
{
    public SqlArrayConstructor(IReadOnlyList<SqlExpression> items)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public IReadOnlyList<SqlExpression> Items { get; }

    public void Accept(ISqlExpressionVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var item in Items)
            item.Accept(visitor);
    }

    public string ToExpressionString() =>
        $"ARRAY[{string.Join(", ", Items.Select(i => i.ToExpressionString()))}]";

    public override string ToString() =>
        $"ARRAY[{string.Join(", ", Items.Select(i => i.ToString()))}]";
}
