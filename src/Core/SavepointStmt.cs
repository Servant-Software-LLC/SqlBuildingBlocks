using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class SavepointStmt : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public SavepointStmt(Grammar grammar, Id id)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));

        var SAVEPOINT = grammar.ToTerm("SAVEPOINT");
        var ROLLBACK = grammar.ToTerm("ROLLBACK");
        var TO = grammar.ToTerm("TO");
        var RELEASE = grammar.ToTerm("RELEASE");

        var savepointOpt = new NonTerminal("savepointOpt");
        savepointOpt.Rule = grammar.Empty | SAVEPOINT;

        var createSavepoint = new NonTerminal("createSavepoint");
        createSavepoint.Rule = SAVEPOINT + id;

        var rollbackToSavepoint = new NonTerminal("rollbackToSavepoint");
        rollbackToSavepoint.Rule = ROLLBACK + TO + savepointOpt + id;

        var releaseSavepoint = new NonTerminal("releaseSavepoint");
        releaseSavepoint.Rule = RELEASE + savepointOpt + id;

        Rule = createSavepoint | rollbackToSavepoint | releaseSavepoint;

        grammar.MarkPunctuation(SAVEPOINT, TO, RELEASE);
    }

    public Id Id { get; }

    public virtual SqlSavepointDefinition Create(ParseTreeNode savepointStmt)
    {
        SqlSavepointDefinition definition = new();
        Update(savepointStmt, definition);
        return definition;
    }

    public virtual void Update(ParseTreeNode savepointStmt, SqlSavepointDefinition definition)
    {
        if (savepointStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {savepointStmt.Term.Name} which does not match {TermName}", nameof(savepointStmt));
        }

        var innerNode = savepointStmt.ChildNodes[0];
        var termName = innerNode.Term.Name;

        if (termName == "createSavepoint")
        {
            definition.Kind = SqlSavepointKind.Create;
            // SAVEPOINT is punctuation, so only id remains
            definition.Name = Id.CreateColumnRef(innerNode.ChildNodes[0]).ColumnName;
        }
        else if (termName == "rollbackToSavepoint")
        {
            definition.Kind = SqlSavepointKind.Rollback;
            // ROLLBACK, TO, SAVEPOINT are punctuation — but ROLLBACK is NOT marked as punctuation
            // Children: ROLLBACK, savepointOpt, id — no, wait...
            // TO and RELEASE are punctuation. SAVEPOINT is punctuation. ROLLBACK is not.
            // rollbackToSavepoint: ROLLBACK + TO + savepointOpt + id
            // After punctuation removal: ROLLBACK + savepointOpt + id
            // But savepointOpt is Empty|SAVEPOINT, and SAVEPOINT is punctuation...
            // Need to handle savepointOpt carefully
            // Actually SAVEPOINT is marked as punctuation globally.
            // So: ROLLBACK remains, TO removed, savepointOpt (if SAVEPOINT, it gets removed too), id
            // The last child should be the id
            var lastChild = innerNode.ChildNodes[innerNode.ChildNodes.Count - 1];
            definition.Name = Id.CreateColumnRef(lastChild).ColumnName;
        }
        else if (termName == "releaseSavepoint")
        {
            definition.Kind = SqlSavepointKind.Release;
            // RELEASE and SAVEPOINT are punctuation, so only id remains
            var lastChild = innerNode.ChildNodes[innerNode.ChildNodes.Count - 1];
            definition.Name = Id.CreateColumnRef(lastChild).ColumnName;
        }
    }
}
