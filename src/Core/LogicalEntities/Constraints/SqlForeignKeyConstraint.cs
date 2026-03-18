namespace SqlBuildingBlocks.LogicalEntities;

public enum ForeignKeyReferentialAction
{
    NoAction,
    Restrict,
    Cascade,
    SetNull,
    SetDefault,
}

public class SqlForeignKeyConstraint
{
    public SqlForeignKeyConstraint(SqlTable parentTable) => ParentTable = parentTable ?? throw new ArgumentNullException(nameof(parentTable));

    public SqlTable ParentTable { get; set; }

    /// <summary>
    /// In most cases, there is only 1 item in this list, but a foreign key constraint can actually reference a composite key, which is a key made up of multiple columns.
    /// </summary>
    public IList<(string Column, string ParentColumn)> ColumnReferences { get; } = new List<(string Column, string ParentColumn)>();

    /// <summary>
    /// The referential action to take when the referenced row is deleted. Null means no explicit action was specified.
    /// </summary>
    public ForeignKeyReferentialAction? OnDeleteAction { get; set; }

    /// <summary>
    /// The referential action to take when the referenced row is updated. Null means no explicit action was specified.
    /// </summary>
    public ForeignKeyReferentialAction? OnUpdateAction { get; set; }
}
