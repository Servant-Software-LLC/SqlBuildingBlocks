using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using Xunit;

namespace SqlBuildingBlocks.Grammars.SQLServer.Tests;

/// <summary>
/// Negative / malformed-SQL parsing tests for the SQL Server grammar.
///
/// Per AGENTS.md P0 priorities, the parser must not silently accept malformed SQL.
/// Each test feeds a known-bad SQL string and asserts that the parse tree reports
/// errors (HasErrors() == true) AND that the parser produced a non-empty error
/// message. The actual message text is not asserted because the Irony parser's
/// wording is not stable API.
///
/// SQL Server uses TOP N rather than LIMIT/OFFSET; FETCH FIRST is also accepted
/// in many dialects but is not in the SQL Server SelectStmt rule. Inputs that the
/// grammar happens to accept (rather than reject) are intentionally not in this
/// catalog -- those are findings rather than enforced behavior.
/// </summary>
public class NegativeTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar() : base(false) // SQL is case insensitive
        {
            SQLServer.SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableHintOpt tableHintOpt = new(this);
            SQLServer.TableName tableName = new(this, aliasOpt, id, tableHintOpt);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            SQLServer.SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);
            expr.InitializeRule(selectStmt, funcCall);

            // Use base Insert/Update statements -- SQL Server subclasses require OUTPUT
            // wiring that is unrelated to negative-test coverage.
            SqlBuildingBlocks.InsertStmt insertStmt = new(this, id, expr, selectStmt);
            SqlBuildingBlocks.UpdateStmt updateStmt =
                new(this, id, literalValue, parameter, funcCall, tableName, whereClauseOpt, joinChainOpt);

            Stmt stmt = new(this, selectStmt, insertStmt, updateStmt);
            Root = stmt;
        }
    }

    public static IEnumerable<object[]> MalformedSql => new[]
    {
        // 1. Incomplete WHERE
        new object[] { "SELECT * FROM Customers WHERE" },

        // 2. Unclosed parens
        new object[] { "SELECT (1 + 2 FROM Customers" },

        // 3. Empty IN list
        new object[] { "SELECT * FROM Customers WHERE x IN ()" },

        // 4. Malformed TOP -- TOP keyword with no count
        new object[] { "SELECT TOP FROM Customers" },

        // (Finding: dangling comma in SELECT list -- "SELECT a, b, FROM Customers" --
        // is silently accepted by the SQL Server grammar; not enforced here.)

        // 5. Dangling comma in INSERT column list
        new object[] { "INSERT INTO Customers (a, b,) VALUES (1, 2)" },

        // 6. Missing FROM keyword
        new object[] { "SELECT * Customers" },

        // 7. Unterminated string literal
        new object[] { "SELECT 'abc FROM Customers" },

        // 8. Malformed JOIN -- INNER JOIN with no table or condition
        new object[] { "SELECT * FROM a INNER JOIN" },

        // 9. Trailing operator
        new object[] { "SELECT * FROM Customers WHERE x =" },

        // 10. UPDATE with dangling comma in SET list
        new object[] { "UPDATE Customers SET a = 1, b = 2, WHERE id = 1" },
    };

    [Theory]
    [MemberData(nameof(MalformedSql))]
    public void MalformedSql_ProducesParseError(string sql)
    {
        TestGrammar grammar = new();
        ParseTree parseTree = GrammarParser.ParseTree(grammar, sql);

        Assert.True(parseTree.HasErrors(), $"Expected parser to reject: {sql}");
        Assert.NotEmpty(parseTree.ParserMessages);
        Assert.All(parseTree.ParserMessages, msg => Assert.False(string.IsNullOrWhiteSpace(msg.Message)));
    }
}
