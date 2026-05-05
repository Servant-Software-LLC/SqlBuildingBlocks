using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using Xunit;

namespace SqlBuildingBlocks.Grammars.MySQL.Tests;

/// <summary>
/// Negative / malformed-SQL parsing tests for the MySQL grammar.
///
/// Per AGENTS.md P0 priorities, the parser must not silently accept malformed SQL.
/// Each test feeds a known-bad SQL string and asserts that the parse tree reports
/// errors (HasErrors() == true) AND that the parser produced a non-empty error
/// message. The actual message text is not asserted because the Irony parser's
/// wording is not stable API.
///
/// Inputs that the grammar happens to accept (rather than reject) are intentionally
/// not in this catalog -- those are findings rather than enforced behavior.
/// </summary>
public class NegativeTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            // MySQL has special naming rules for identifiers (backtick).
            MySQL.SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            MySQL.Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            MySQL.SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);
            expr.InitializeRule(selectStmt, funcCall);
            expr.AddIntervalSupport(this);

            InsertStmt insertStmt = new(this, id, expr, selectStmt);
            UpdateStmt updateStmt = new(this, id, literalValue, parameter, funcCall, tableName, whereClauseOpt, joinChainOpt);

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

        // 4. Malformed LIMIT -- LIMIT keyword with no number
        new object[] { "SELECT * FROM Customers LIMIT" },

        // 5. Malformed LIMIT -- trailing comma
        new object[] { "SELECT * FROM Customers LIMIT 5," },

        // (Finding: dangling comma in SELECT list -- "SELECT a, b, FROM Customers" --
        // is silently accepted by the MySQL grammar; not enforced here.)

        // 6. Dangling comma in INSERT column list
        new object[] { "INSERT INTO Customers (a, b,) VALUES (1, 2)" },

        // 7. Missing FROM keyword
        new object[] { "SELECT * Customers" },

        // 8. Unterminated string literal
        new object[] { "SELECT 'abc FROM Customers" },

        // 9. Malformed JOIN -- INNER JOIN with no table or condition
        new object[] { "SELECT * FROM a INNER JOIN" },

        // 10. Trailing operator
        new object[] { "SELECT * FROM Customers WHERE x =" },

        // 11. UPDATE with dangling comma in SET list
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
