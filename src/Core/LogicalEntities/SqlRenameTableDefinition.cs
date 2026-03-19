namespace SqlBuildingBlocks.LogicalEntities;

public class SqlRenameTableDefinition
{
    public SqlTable? SourceTable { get; set; }

    public SqlTable? TargetTable { get; set; }
}
