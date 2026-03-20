using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class AlterViewStmt : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public AlterViewStmt(Grammar grammar, Id id, SelectStmt selectStmt)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        SelectStmt = selectStmt ?? throw new ArgumentNullException(nameof(selectStmt));

        var ALTER = grammar.ToTerm("ALTER");
        var VIEW = grammar.ToTerm("VIEW");
        var AS = grammar.ToTerm("AS");

        Rule = ALTER + VIEW + id + AS + selectStmt;

        grammar.MarkPunctuation(ALTER, VIEW, AS);
    }

    public Id Id { get; }
    public SelectStmt SelectStmt { get; }

    public virtual SqlAlterViewDefinition Create(ParseTreeNode alterViewStmt)
    {
        SqlAlterViewDefinition definition = new();
        Update(alterViewStmt, definition);
        return definition;
    }

    public virtual void Update(ParseTreeNode alterViewStmt, SqlAlterViewDefinition definition)
    {
        if (alterViewStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {alterViewStmt.Term.Name} which does not match {TermName}", nameof(alterViewStmt));
        }

        // After ALTER, VIEW, AS are removed as punctuation:
        //   ChildNodes[0] = id (view name)
        //   ChildNodes[1] = selectStmt
        definition.View = Id.CreateTable(alterViewStmt.ChildNodes[0]);
        definition.AsSelect = SelectStmt.Create(alterViewStmt.ChildNodes[1]);
    }
}
