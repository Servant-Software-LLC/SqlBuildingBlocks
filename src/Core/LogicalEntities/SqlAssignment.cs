namespace SqlBuildingBlocks.LogicalEntities;

public class SqlAssignment
{
    public SqlAssignment(SqlColumn column, SqlExpression expression)
    {
        Column = column ?? throw new ArgumentNullException(nameof(column));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    public SqlAssignment(SqlColumn column, SqlLiteralValue literalValue)
        : this(column, new SqlExpression(literalValue)) { }

    public SqlAssignment(SqlColumn column, SqlParameter parameter)
        : this(column, new SqlExpression(parameter)) { }

    public SqlAssignment(SqlColumn column, SqlFunction function)
        : this(column, new SqlExpression(function)) { }


    public SqlColumn Column { get; set; }

    public SqlExpression Expression { get; }

    //Backward-compatible convenience properties derived from Expression.
    public SqlLiteralValue? Value => Expression.Value;
    public SqlParameter? Parameter => Expression.Parameter;
    public SqlFunction? Function => Expression.Function;
}
