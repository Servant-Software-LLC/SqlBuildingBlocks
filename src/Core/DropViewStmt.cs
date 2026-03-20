using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class DropViewStmt : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public DropViewStmt(Grammar grammar, Id id)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));

        var COMMA = grammar.ToTerm(",");
        var DROP = grammar.ToTerm("DROP");
        var VIEW = grammar.ToTerm("VIEW");
        var IF = grammar.ToTerm("IF");
        var EXISTS = grammar.ToTerm("EXISTS");

        var ifExistsOpt = new NonTerminal("ifExistsOpt");
        ifExistsOpt.Rule = grammar.Empty | IF + EXISTS;

        var viewList = new NonTerminal("dropViewList");
        viewList.Rule = grammar.MakePlusRule(viewList, COMMA, id);

        Rule = DROP + VIEW + ifExistsOpt + viewList;

        grammar.MarkPunctuation(DROP, VIEW);
    }

    public Id Id { get; }

    public virtual SqlDropViewDefinition Create(ParseTreeNode dropViewStmt)
    {
        SqlDropViewDefinition definition = new();
        Update(dropViewStmt, definition);
        return definition;
    }

    public virtual void Update(ParseTreeNode dropViewStmt, SqlDropViewDefinition definition)
    {
        if (dropViewStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {dropViewStmt.Term.Name} which does not match {TermName}", nameof(dropViewStmt));
        }

        var ifExistsNode = dropViewStmt.ChildNodes[0];
        definition.IfExists = ifExistsNode.ChildNodes.Count > 0;

        var viewListNode = dropViewStmt.ChildNodes[1];
        foreach (ParseTreeNode viewNode in viewListNode.ChildNodes)
        {
            definition.Views.Add(Id.CreateTable(viewNode));
        }
    }
}
