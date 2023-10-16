namespace SqlBuildingBlocks.LogicalEntities;

public class SqlForeignKeyConstraint
{
    public SqlForeignKeyConstraint(SqlTable parentTable) => ParentTable = parentTable ?? throw new ArgumentNullException(nameof(parentTable));

    public SqlTable ParentTable { get; set; }

    /// <summary>
    /// In most cases, there is only 1 item in this list, but a foreign key constraint can actually reference a composite key, which is a key made up of multiple columns.
    /// </summary>
    public IList<(string Column, string ParentColumn)> ColumnReferences { get; } = new List<(string Column, string ParentColumn)> ();
}
