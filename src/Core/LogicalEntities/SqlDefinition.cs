using SqlBuildingBlocks.Interfaces;
using System.Data.Common;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlDefinition
{
    public SqlDefinition(SqlSelectDefinition select) => Select = select ?? throw new ArgumentNullException(nameof(select));
    public SqlDefinition(SqlInsertDefinition insert) => Insert = insert ?? throw new ArgumentNullException(nameof(insert));

    public SqlDefinition(SqlUpdateDefinition update) => Update = update?? throw new ArgumentNullException(nameof(update));
    public SqlDefinition(SqlDeleteDefinition delete) => Delete = delete ?? throw new ArgumentNullException(nameof(delete));
    public SqlDefinition(SqlCreateTableDefinition create) => Create = create ?? throw new ArgumentNullException(nameof(create));
    public SqlDefinition(SqlAlterTableDefinition alter) => Alter = alter ?? throw new ArgumentNullException(nameof(alter));
    public SqlDefinition(SqlDropTableDefinition drop) => Drop = drop ?? throw new ArgumentNullException(nameof(drop));
    public SqlDefinition(SqlRenameTableDefinition rename) => Rename = rename ?? throw new ArgumentNullException(nameof(rename));
    public SqlDefinition(SqlMergeDefinition merge) => Merge = merge ?? throw new ArgumentNullException(nameof(merge));
    public SqlDefinition(SqlCreateViewDefinition createView) => CreateView = createView ?? throw new ArgumentNullException(nameof(createView));
    public SqlDefinition(SqlDropViewDefinition dropView) => DropView = dropView ?? throw new ArgumentNullException(nameof(dropView));
    public SqlDefinition(SqlAlterViewDefinition alterView) => AlterView = alterView ?? throw new ArgumentNullException(nameof(alterView));
    public SqlDefinition(SqlCreateIndexDefinition createIndex) => CreateIndex = createIndex ?? throw new ArgumentNullException(nameof(createIndex));
    public SqlDefinition(SqlDropIndexDefinition dropIndex) => DropIndex = dropIndex ?? throw new ArgumentNullException(nameof(dropIndex));
    public SqlDefinition(SqlTransactionDefinition transaction) => Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    public SqlDefinition(SqlSavepointDefinition savepoint) => Savepoint = savepoint ?? throw new ArgumentNullException(nameof(savepoint));

    //Only one of the following properties will ever be set.  Bounded by the ctors.
    public SqlSelectDefinition? Select { get; }
    public SqlInsertDefinition? Insert { get; }
    public SqlUpdateDefinition? Update { get; }
    public SqlDeleteDefinition? Delete { get;}
    public SqlCreateTableDefinition? Create { get; }
    public SqlAlterTableDefinition? Alter { get; }
    public SqlDropTableDefinition? Drop { get; }
    public SqlRenameTableDefinition? Rename { get; }
    public SqlMergeDefinition? Merge { get; }
    public SqlCreateViewDefinition? CreateView { get; }
    public SqlDropViewDefinition? DropView { get; }
    public SqlAlterViewDefinition? AlterView { get; }
    public SqlCreateIndexDefinition? CreateIndex { get; }
    public SqlDropIndexDefinition? DropIndex { get; }
    public SqlTransactionDefinition? Transaction { get; }
    public SqlSavepointDefinition? Savepoint { get; }

    public void ResolveParameters(DbParameterCollection parameters)
    {
        Select?.ResolveParameters(parameters);
        Insert?.ResolveParameters(parameters);
        Update?.ResolveParameters(parameters);
        Delete?.ResolveParameters(parameters);

        //NOTE: CREATE TABLE does not use parameters
        //NOTE: ALTER TABLE does not use parameters
        //NOTE: RENAME TABLE does not use parameters
    }

    /// <summary>
    /// Resolve functions which don't depend on the state of individual rows (for instance, LAST_INSERT_ID() depends on the previous SQL INSERT statement to determine its value whereas UPPER() would depend on the current row that is being evaluated.)
    /// </summary>
    /// <param name="functionProvider"></param>
    public void ResolveFunction(IFunctionProvider functionProvider)
    {
        Select?.ResolveFunctions(functionProvider);
        Insert?.ResolveFunctions(functionProvider);
        Update?.ResolveFunctions(functionProvider);
        Delete?.ResolveFunctions(functionProvider);

        //NOTE: CREATE TABLE does not use functions
        //NOTE: ALTER TABLE does not use functions
        //NOTE: RENAME TABLE does not use functions
    }

    public override string ToString()
    {
        if (Select != null) return Select.ToString(); 
        if (Insert != null) return Insert.ToString(); 
        if (Update != null) return Update.ToString();
        if (Delete != null) return Delete.ToString();
        if (Create != null) return Create.ToString();
        if (Alter != null) return Alter.ToString();
        if (Drop != null) return Drop.ToString();
        if (Rename != null) return Rename.ToString();
        if (Merge != null) return Merge.ToString();
        if (CreateView != null) return "CREATE VIEW";
        if (DropView != null) return "DROP VIEW";
        if (AlterView != null) return "ALTER VIEW";
        if (CreateIndex != null) return "CREATE INDEX";
        if (DropIndex != null) return "DROP INDEX";
        if (Transaction != null) return Transaction.Kind.ToString().ToUpperInvariant();
        if (Savepoint != null) return "SAVEPOINT";

        return "SQL definition type not set";
    }
}
