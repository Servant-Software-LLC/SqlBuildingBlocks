using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class DropIndexStmt : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public DropIndexStmt(Grammar grammar, Id id)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));

        var DROP = grammar.ToTerm("DROP");
        var INDEX = grammar.ToTerm("INDEX");
        var IF = grammar.ToTerm("IF");
        var EXISTS = grammar.ToTerm("EXISTS");

        var ifExistsOpt = new NonTerminal("ifExistsOpt");
        ifExistsOpt.Rule = grammar.Empty | IF + EXISTS;

        Rule = DROP + INDEX + ifExistsOpt + id;

        grammar.MarkPunctuation(DROP, INDEX);
    }

    public Id Id { get; }

    public virtual SqlDropIndexDefinition Create(ParseTreeNode dropIndexStmt)
    {
        SqlDropIndexDefinition definition = new();
        Update(dropIndexStmt, definition);
        return definition;
    }

    public virtual void Update(ParseTreeNode dropIndexStmt, SqlDropIndexDefinition definition)
    {
        if (dropIndexStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {dropIndexStmt.Term.Name} which does not match {TermName}", nameof(dropIndexStmt));
        }

        // After DROP, INDEX are removed as punctuation:
        //   ChildNodes[0] = ifExistsOpt
        //   ChildNodes[1] = id (index name)
        var ifExistsNode = dropIndexStmt.ChildNodes[0];
        definition.IfExists = ifExistsNode.ChildNodes.Count > 0;

        definition.IndexName = Id.CreateColumnRef(dropIndexStmt.ChildNodes[1]).ColumnName;
    }
}
