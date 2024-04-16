using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using Xunit;

namespace SqlBuildingBlocks.Grammars.SQLServer.Tests;

public class CaseInsensitiveTests
{
    // Loosely based on SQL89 grammar from Gold parser. Supports some extra TSQL constructs.
    [Language("SQL", "89", "SQL 89 grammar")]
    public class SqlGrammar : Grammar
    {
        public SqlGrammar() : base(false) //SQL is case insensitive
        {
            //Comment.Register(this);

            //NOTE: Using SQL Server's naming scheme.
            SQLServer.SimpleId simpleId = new(this);
            Id id = new(this, simpleId);

            SelectStmt selectStmt = new(this, id);

            selectStmt.Expr.InitializeRule(selectStmt, selectStmt.FuncCall);

            Root = selectStmt;
        }

        //public virtual IEnumerable<SqlDefinition> Create(ParseTreeNode stmtList) =>
        //    ((SelectStmt)Root).Create(stmtList);

    }


    /// <summary>
    /// Test to avoid issues in LiteralValue when using CaseInsensitive grammar\
    /// Errors were like:
    ///     Message:  Reduce-reduce conflict. State S49, lookaheads: + - * / % & | ^ = > < >= <= <> != !< !> AND OR LIKE NOT IN ) , GROUP HAVING ORDER EOF INNER LEFT RIGHT JOIN WHERE. Selected reduce on first production in conflict set. (S49) Reduce-reduce conflict. State S50, lookaheads: + - * / % & | ^ = > < >= <= <> != !< !> AND OR LIKE NOT IN ) , GROUP HAVING ORDER EOF INNER LEFT RIGHT JOIN WHERE. Selected reduce on first production in conflict set. (S50) 
    /// </summary>
    [Fact]
    public void FailedAtParsing_DueToDuplicateWordsOfDifferingCase_InLiteralValueRules_Test()
    {
        var grammar = new SqlGrammar();
        var commandText = "SELECT BlogId FROM Blogs";
        var parseTreeNode = GrammarParser.Parse(grammar, commandText);

        Assert.NotNull(parseTreeNode);
    }
}
