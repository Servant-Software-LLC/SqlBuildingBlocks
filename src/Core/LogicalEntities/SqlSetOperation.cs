namespace SqlBuildingBlocks.LogicalEntities;

public class SqlSetOperation
{
    public SqlSetOperation(SqlSetOperator operatorType, SqlSelectDefinition right)
    {
        Operator = operatorType;
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public SqlSetOperator Operator { get; }
    public SqlSelectDefinition Right { get; }
}
