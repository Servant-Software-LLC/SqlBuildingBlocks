namespace SqlBuildingBlocks.LogicalEntities;

public class SqlDerivedTable : SqlTable
{
    public SqlDerivedTable(SqlSelectDefinition selectDefinition, string alias)
        : base(null, alias)
    {
        SelectDefinition = selectDefinition ?? throw new ArgumentNullException(nameof(selectDefinition));
        TableAlias = alias;
    }

    public SqlSelectDefinition SelectDefinition { get; }

    public override string ToString() => $"(<subquery>) AS {TableAlias}";
}
