using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class StmtLine : NonTerminal
{
    public StmtLine(Grammar grammar) : this(grammar, new Stmt(grammar)) { }

    public StmtLine(Grammar grammar, Stmt stmt)
        : base(TermName)
    {
        Stmt = stmt ?? throw new ArgumentNullException(nameof(stmt));

        var comment = new CommentTerminal("comment", "/*", "*/");
        var lineComment = new CommentTerminal("line_comment", "--", "\n", "\r\n");

        var semiOpt = new NonTerminal("semiOpt");
        var stmtLine = new NonTerminal("stmtLine");
        var stmtList = new NonTerminal("stmtList");

        semiOpt.Rule = grammar.Empty | ";";
        stmtLine.Rule = stmt + semiOpt;
        stmtList.Rule = grammar.MakePlusRule(stmtList, stmtLine);

        Rule = stmtList;

        grammar.NonGrammarTerminals.Add(comment);
        grammar.NonGrammarTerminals.Add(lineComment);
        grammar.MarkPunctuation(semiOpt);
        grammar.MarkTransient(this);
    }

    public Stmt Stmt { get; }

    public virtual IEnumerable<SqlDefinition> Create(ParseTreeNode stmtList) =>
        Create(stmtList, Stmt.Create);


    public virtual IEnumerable<SqlDefinition> Create(ParseTreeNode stmtList, IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider,
                                    IFunctionProvider? functionProvider = null) => 
        Create(stmtList, stmtLine => Stmt.Create(stmtLine, databaseConnectionProvider, tableSchemaProvider, functionProvider));


    private IEnumerable<SqlDefinition> Create(ParseTreeNode stmtList, Func<ParseTreeNode, SqlDefinition> createFunc)
    {
        foreach (var stmtLine in stmtList.ChildNodes)
        {
            yield return createFunc(stmtLine.ChildNodes[0]);
        }
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
