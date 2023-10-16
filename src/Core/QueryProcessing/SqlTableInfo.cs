using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.QueryProcessing;

public class SqlTableInfo
{
    public SqlTableInfo(SqlTable table)
    {
        Table = table ?? throw new ArgumentNullException(nameof(table));
    }

    public SqlTable Table { get; }

    public long? TableRows { get; set; }
    public DateTime Created { get; set; }
}
