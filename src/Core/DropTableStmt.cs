using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class DropTableStmt : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor.
    /// </summary>
    /// <param name="grammar"></param>
    public DropTableStmt(Grammar grammar) : this(grammar, new Id(grammar)) { }

    public DropTableStmt(Grammar grammar, Id id)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));

        var COMMA = grammar.ToTerm(",");
        var DROP = grammar.ToTerm("DROP");
        var TABLE = grammar.ToTerm("TABLE");
        var IF = grammar.ToTerm("IF");
        var EXISTS = grammar.ToTerm("EXISTS");

        // ifExistsOpt is NOT marked transient so it always occupies ChildNodes[0];
        // when matched as Empty it has no child tokens, so .ChildNodes.Count == 0.
        var ifExistsOpt = new NonTerminal("ifExistsOpt");
        ifExistsOpt.Rule = grammar.Empty | IF + EXISTS;

        var tableList = new NonTerminal("dropTableList");
        tableList.Rule = grammar.MakePlusRule(tableList, COMMA, id);

        Rule = DROP + TABLE + ifExistsOpt + tableList;

        grammar.MarkPunctuation(DROP, TABLE);
        // IF and EXISTS are intentionally NOT marked as punctuation so their presence
        // can be detected via ifExistsOpt.ChildNodes.Count > 0.
    }

    public Id Id { get; }

    public virtual SqlDropTableDefinition Create(ParseTreeNode dropTableStmt)
    {
        SqlDropTableDefinition sqlDropTableDefinition = new();
        Update(dropTableStmt, sqlDropTableDefinition);
        return sqlDropTableDefinition;
    }

    public virtual void Update(ParseTreeNode dropTableStmt, SqlDropTableDefinition sqlDropTableDefinition)
    {
        if (dropTableStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {dropTableStmt.Term.Name} which does not match {TermName}", nameof(dropTableStmt));
        }

        // After DROP and TABLE are removed as punctuation, the remaining children are:
        //   ChildNodes[0] = ifExistsOpt  (empty or IF + EXISTS tokens)
        //   ChildNodes[1] = dropTableList
        var ifExistsNode = dropTableStmt.ChildNodes[0];
        sqlDropTableDefinition.IfExists = ifExistsNode.ChildNodes.Count > 0;

        var tableListNode = dropTableStmt.ChildNodes[1];
        foreach (ParseTreeNode tableNode in tableListNode.ChildNodes)
        {
            sqlDropTableDefinition.Tables.Add(Id.CreateTable(tableNode));
        }
    }
}
