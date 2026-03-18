using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents the parenthesised value list on the right-hand side of an IN / NOT IN predicate,
/// e.g. the <c>(1, 2, 3)</c> in <c>status IN (1, 2, 3)</c>.
/// </summary>
public class SqlInList
{
    public SqlInList(IReadOnlyList<SqlExpression> items)
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
        $"({string.Join(", ", Items.Select(i => i.ToExpressionString()))})";

    public override string ToString() =>
        $"({string.Join(", ", Items.Select(i => i.ToString()))})";
}
