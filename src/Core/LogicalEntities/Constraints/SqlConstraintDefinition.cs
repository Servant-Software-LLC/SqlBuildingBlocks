namespace SqlBuildingBlocks.LogicalEntities;

public class SqlConstraintDefinition
{
    public SqlConstraintDefinition(string name, SqlPrimaryKeyConstraint sqlPrimaryKeyConstraint) : this(name) =>
        PrimaryKeyConstraint = sqlPrimaryKeyConstraint ?? throw new ArgumentNullException(nameof(sqlPrimaryKeyConstraint));
    public SqlConstraintDefinition(string name, SqlUniqueConstraint sqlUniqueConstraint) : this(name) =>
        UniqueConstraint = sqlUniqueConstraint ?? throw new ArgumentNullException(nameof(sqlUniqueConstraint));
    public SqlConstraintDefinition(string name, SqlForeignKeyConstraint sqlForeignKeyConstraint) : this(name) =>
        ForeignKeyConstraint = sqlForeignKeyConstraint ?? throw new ArgumentNullException(nameof(sqlForeignKeyConstraint));
    private SqlConstraintDefinition(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

    public string Name { get; }

    //Only one of the following properties will ever be set.  Bounded by the ctors.
    public SqlPrimaryKeyConstraint? PrimaryKeyConstraint { get; }
    public SqlUniqueConstraint? UniqueConstraint { get; }
    public SqlForeignKeyConstraint? ForeignKeyConstraint { get; }
}
