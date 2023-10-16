namespace SqlBuildingBlocks.LogicalEntities;

public class SqlReturning
{
    public SqlReturning(SqlColumn column) => Column = column;
    public SqlReturning(int integer) => Int = integer;

    public SqlColumn? Column { get; }

    public int? Int { get; }
}
