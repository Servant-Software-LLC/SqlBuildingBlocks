using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class CreateViewStmt : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public CreateViewStmt(Grammar grammar, Id id, SelectStmt selectStmt)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        SelectStmt = selectStmt ?? throw new ArgumentNullException(nameof(selectStmt));

        var CREATE = grammar.ToTerm("CREATE");
        var OR = grammar.ToTerm("OR");
        var REPLACE = grammar.ToTerm("REPLACE");
        var VIEW = grammar.ToTerm("VIEW");
        var AS = grammar.ToTerm("AS");

        var orReplaceOpt = new NonTerminal("orReplaceOpt");
        orReplaceOpt.Rule = grammar.Empty | OR + REPLACE;

        Rule = CREATE + orReplaceOpt + VIEW + id + AS + selectStmt;

        grammar.MarkPunctuation(CREATE, VIEW, AS);
    }

    public Id Id { get; }
    public SelectStmt SelectStmt { get; }

    public virtual SqlCreateViewDefinition Create(ParseTreeNode createViewStmt)
    {
        SqlCreateViewDefinition definition = new();
        Update(createViewStmt, definition);
        return definition;
    }

    public virtual void Update(ParseTreeNode createViewStmt, SqlCreateViewDefinition definition)
    {
        if (createViewStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {createViewStmt.Term.Name} which does not match {TermName}", nameof(createViewStmt));
        }

        // After CREATE, VIEW, AS are removed as punctuation:
        //   ChildNodes[0] = orReplaceOpt (empty or OR + REPLACE)
        //   ChildNodes[1] = id (view name)
        //   ChildNodes[2] = selectStmt
        var orReplaceNode = createViewStmt.ChildNodes[0];
        definition.OrReplace = orReplaceNode.ChildNodes.Count > 0;

        definition.View = Id.CreateTable(createViewStmt.ChildNodes[1]);
        definition.AsSelect = SelectStmt.Create(createViewStmt.ChildNodes[2]);
    }
}
