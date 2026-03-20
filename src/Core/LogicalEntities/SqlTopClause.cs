using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlTopClause
{
    public SqlLimitValue Count { get; set; } = new();

    public bool Percent { get; set; }

    public bool WithTies { get; set; }

    public void Accept(ISqlValueVisitor visitor)
    {
        Count?.Accept(visitor);
    }
}
