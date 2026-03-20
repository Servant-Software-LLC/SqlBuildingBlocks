namespace SqlBuildingBlocks.LogicalEntities;

public class SqlAlterViewDefinition
{
    /// <summary>
    /// The name of the view (may include database/schema qualifier).
    /// </summary>
    public SqlTable? View { get; set; }

    /// <summary>
    /// The new SELECT definition that forms the body of the view.
    /// </summary>
    public SqlSelectDefinition? AsSelect { get; set; }
}
