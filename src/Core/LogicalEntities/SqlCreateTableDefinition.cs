namespace SqlBuildingBlocks.LogicalEntities;

public class SqlCreateTableDefinition
{
    /// <summary>
    /// Full name of the table to be created.  <see cref="SqlTable.TableAlias"/> will not be set.
    /// </summary>
    public SqlTable Table { get; set; }

    public IList<SqlColumnDefinition> Columns { get; private set; } = new List<SqlColumnDefinition>();

    //NOTE:  Although depening on the database, NOT NULL constraint can either be inline with the column
    //       definition or defined as a separate named constraint, to simplify things for the end user,
    //       logical entities keeps this concept on the SqlColumnDefinition instead of on this property.
    public IList<SqlConstraintDefinition> Constraints { get; private set; } = new List<SqlConstraintDefinition>();
}
