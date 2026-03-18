namespace SqlBuildingBlocks.LogicalEntities;

public class SqlCheckConstraint
{
    public SqlCheckConstraint(SqlExpression expression) => Expression = expression ?? throw new ArgumentNullException(nameof(expression));

    public SqlExpression Expression { get; }
}
