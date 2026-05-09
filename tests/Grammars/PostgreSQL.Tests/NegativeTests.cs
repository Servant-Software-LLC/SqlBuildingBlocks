using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using Xunit;

namespace SqlBuildingBlocks.Grammars.PostgreSQL.Tests;

/// <summary>
/// Negative / malformed-SQL parsing tests for the PostgreSQL grammar.
///
/// Per AGENTS.md P0 priorities, the parser must not silently accept malformed SQL.
/// Each test feeds a known-bad SQL string and asserts that the parse tree reports
/// errors (HasErrors() == true) AND that the parser produced a non-empty error
/// message. The actual message text is not asserted because the Irony parser's
/// wording is not stable API.
///
/// PostgreSQL grammar is currently a stub that reuses the base Core SelectStmt;
/// PostgreSQL adds its own InsertStmt for ON CONFLICT support. Inputs that the
/// grammar happens to accept (rather than reject) are intentionally not in this
/// catalog -- those are findings rather than enforced behavior.
/// </summary>
public class NegativeTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);
            expr.InitializeRule(selectStmt, funcCall);

            PostgreSQL.InsertStmt insertStmt = new(this, id, expr, selectStmt);
            // Use base UpdateStmt -- PostgreSQL.UpdateStmt requires a custom RETURNING wiring
            // that is unrelated to negative-test coverage.
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

        // 4. Dangling comma in SELECT list (issue #167) -- now rejected by the
        //    PostgreSQL grammar because FROM/INTO are reserved words and cannot
        //    be consumed as identifiers after a trailing comma in the column list.
        new object[] { "SELECT a, b, FROM Customers" },

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

        // 10. ON CONFLICT keyword with no body
        new object[] { "INSERT INTO Customers (a, b) VALUES (1, 2) ON CONFLICT" },

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
