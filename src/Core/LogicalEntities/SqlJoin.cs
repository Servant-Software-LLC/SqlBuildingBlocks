namespace SqlBuildingBlocks.LogicalEntities;

public class SqlJoin
{
    public SqlJoin(SqlTable table, SqlBinaryExpression condition)
    {
        Table = table ?? throw new ArgumentNullException(nameof(table));
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    public SqlJoinKind JoinKind { get; set; } = SqlJoinKind.Inner;

    public SqlTable Table { get; set; }

    public SqlBinaryExpression Condition { get; set; }

}
