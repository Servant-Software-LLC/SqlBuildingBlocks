using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlLimitOffset
{
    public SqlLimitValue RowCount { get; set; } = new();

    public SqlLimitValue RowOffset { get; set; } = new();

    public void Accept(ISqlValueVisitor visitor)
    {
        RowCount?.Accept(visitor);
        RowOffset?.Accept(visitor);
    }
}
