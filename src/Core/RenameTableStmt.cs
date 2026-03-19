using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class RenameTableStmt : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public RenameTableStmt(Grammar grammar)
        : this(grammar, new Id(grammar)) { }

    public RenameTableStmt(Grammar grammar, Id id)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));

        var ALTER = grammar.ToTerm("ALTER");
        var RENAME = grammar.ToTerm("RENAME");
        var TABLE = grammar.ToTerm("TABLE");
        var TO = grammar.ToTerm("TO");

        Rule = ALTER + TABLE + id + RENAME + TO + id
             | RENAME + TABLE + id + TO + id;

        grammar.MarkPunctuation("ALTER", "RENAME", "TABLE", "TO");
    }

    public Id Id { get; }

    public virtual SqlRenameTableDefinition Create(ParseTreeNode renameTableStmt)
    {
        SqlRenameTableDefinition sqlRenameTableDefinition = new();
        Update(renameTableStmt, sqlRenameTableDefinition);
        return sqlRenameTableDefinition;
    }

    public virtual void Update(ParseTreeNode renameTableStmt, SqlRenameTableDefinition sqlRenameTableDefinition)
    {
        if (renameTableStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {renameTableStmt.Term.Name} which does not match {TermName}", nameof(renameTableStmt));
        }

        var tableNodes = new List<ParseTreeNode>();
        CollectTableNodes(renameTableStmt, tableNodes);

        sqlRenameTableDefinition.SourceTable = CreateTable(tableNodes[0]);
        sqlRenameTableDefinition.TargetTable = CreateTable(tableNodes[1]);
    }

    private SqlTable CreateTable(ParseTreeNode tableNode)
    {
        if (tableNode.Term.Name == Id.TermName)
            return Id.CreateTable(tableNode);

        if (tableNode.Term.Name == SimpleId.TermName)
            return new(null, Id.SimpleId.Create(tableNode));

        if (tableNode.Token != null)
            return new(null, tableNode.Token.ValueString);

        if (tableNode.ChildNodes.Count == 1)
            return CreateTable(tableNode.ChildNodes[0]);

        throw new ArgumentException($"Cannot create a table from node {tableNode.Term.Name}", nameof(tableNode));
    }

    private void CollectTableNodes(ParseTreeNode node, IList<ParseTreeNode> tableNodes)
    {
        if (node.Term.Name == Id.TermName || node.Term.Name == SimpleId.TermName || node.Token != null)
        {
            tableNodes.Add(node);
            return;
        }

        foreach (var childNode in node.ChildNodes)
            CollectTableNodes(childNode, tableNodes);
    }
}
