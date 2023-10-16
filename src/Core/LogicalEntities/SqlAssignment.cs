namespace SqlBuildingBlocks.LogicalEntities;

public class SqlAssignment
{
    public SqlAssignment(SqlColumn column, SqlLiteralValue literalValue)
    {
        Column = column ?? throw new ArgumentNullException(nameof(column));
        Value = literalValue ?? throw new ArgumentNullException(nameof(literalValue));
    }

    public SqlAssignment(SqlColumn column, SqlParameter parameter)
    {
        Column = column ?? throw new ArgumentNullException(nameof(column));
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
    }

    public SqlAssignment(SqlColumn column, SqlFunction function)
    {
        Column = column ?? throw new ArgumentNullException(nameof(column));
        Function = function ?? throw new ArgumentNullException(nameof(function));
    }


    public SqlColumn Column { get; set; }

    //Only one of the following properties will ever be set.  Restricted by the ctors.
    public SqlLiteralValue? Value { get; }
    public SqlParameter? Parameter { get; }
    public SqlFunction? Function { get; }
}
