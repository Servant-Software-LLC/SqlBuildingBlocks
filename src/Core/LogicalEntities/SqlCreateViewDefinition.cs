namespace SqlBuildingBlocks.LogicalEntities;

public class SqlCreateViewDefinition
{
    /// <summary>
    /// The name of the view (may include database/schema qualifier).
    /// </summary>
    public SqlTable? View { get; set; }

    /// <summary>
    /// Whether this is a CREATE OR REPLACE VIEW statement.
    /// </summary>
    public bool OrReplace { get; set; }

    /// <summary>
    /// The SELECT definition that forms the body of the view.
    /// </summary>
    public SqlSelectDefinition? AsSelect { get; set; }
}
