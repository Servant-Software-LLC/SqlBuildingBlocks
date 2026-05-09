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
/// Issue #175 (Wave 9, 2026-05-09): expanded from the Wave 2 / Wave 6 baseline to
/// ≥50 cases per dialect. The shared audit doc lives at
/// <c>Docs/Audit/silent-accept-corpus-2026.md</c>.
///
/// SQL Server uses TOP N rather than LIMIT/OFFSET; FETCH FIRST is also accepted
/// in many dialects but is not in the SQL Server SelectStmt rule. Bare
/// <c>SELECT *</c> with no FROM is intentional (FROM is optional).
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

    public static IEnumerable<object[]> StatementLevel => new[]
    {
        new object[] { "" },
        new object[] { "   " },
        new object[] { ";" },
        new object[] { "-- nothing" },
        new object[] { "FROM Customers" },
        new object[] { "WHERE x = 1" },
        new object[] { "SELECT 1; SELECT 2" },
    };

    public static IEnumerable<object[]> SelectClause => new[]
    {
        new object[] { "SELECT FROM t" },
        new object[] { "SELECT , FROM t" },
        new object[] { "SELECT *, x FROM t" },
        new object[] { "SELECT x, * FROM t" },
        new object[] { "SELECT DISTINCT ALL x FROM t" },
        new object[] { "SELECT ALL DISTINCT x FROM t" },
        new object[] { "SELECT a FROM t GROUP BY" },
        new object[] { "SELECT a FROM t GROUP BY a," },
        new object[] { "SELECT a FROM t ORDER BY" },
        new object[] { "SELECT a FROM t ORDER BY a," },
        new object[] { "SELECT a FROM t HAVING" },
        new object[] { "SELECT a FROM t WHERE x = 1 GROUP BY a HAVING" },
        new object[] { "SELECT a, FROM Customers" },
        new object[] { "SELECT * FROM Customers WHERE" },
        new object[] { "SELECT * Customers" },
    };

    public static IEnumerable<object[]> FromAndJoin => new[]
    {
        new object[] { "SELECT * FROM" },
        new object[] { "SELECT * FROM a, b," },
        new object[] { "SELECT * FROM a, , b" },
        new object[] { "SELECT * FROM a JOIN b ON" },
        new object[] { "SELECT * FROM a JOIN b ON x = " },
        new object[] { "SELECT * FROM a INNER JOIN" },
        new object[] { "SELECT * FROM a LEFT JOIN" },
        new object[] { "SELECT * FROM a RIGHT JOIN" },
        new object[] { "SELECT * FROM a FULL JOIN b" },
    };

    public static IEnumerable<object[]> Expressions => new[]
    {
        new object[] { "SELECT (1 + 2 FROM Customers" },
        new object[] { "SELECT * FROM Customers WHERE x IN ()" },
        new object[] { "SELECT * FROM t WHERE x BETWEEN 1 AND" },
        new object[] { "SELECT * FROM t WHERE x BETWEEN AND 5" },
        new object[] { "SELECT * FROM t WHERE x BETWEEN 1" },
        new object[] { "SELECT * FROM t WHERE x IS NULL NULL" },
        new object[] { "SELECT * FROM t WHERE x IS" },
        new object[] { "SELECT * FROM t WHERE CASE END" },
        new object[] { "SELECT * FROM t WHERE CASE WHEN x THEN y" },
        new object[] { "SELECT * FROM t WHERE CASE WHEN x END" },
        new object[] { "SELECT * FROM t WHERE MAX(x" },
        new object[] { "SELECT * FROM t WHERE MAX x)" },
        new object[] { "SELECT * FROM Customers WHERE x =" },
        new object[] { "SELECT * FROM t WHERE = 1" },
        new object[] { "SELECT * FROM t WHERE x LIKE" },
        new object[] { "SELECT * FROM t WHERE x IN (,)" },
        new object[] { "SELECT * FROM t WHERE x IN (1,)" },
        new object[] { "SELECT * FROM t WHERE x IN (,1)" },
        new object[] { "SELECT * FROM t WHERE x AND" },
        new object[] { "SELECT * FROM t WHERE AND x = 1" },
        new object[] { "SELECT * FROM t WHERE x = 1 OR" },
        new object[] { "SELECT * FROM t WHERE NOT" },
        new object[] { "SELECT * FROM t WHERE EXISTS" },
        new object[] { "SELECT * FROM t WHERE EXISTS ()" },
    };

    public static IEnumerable<object[]> IdentifiersAndLiterals => new[]
    {
        new object[] { "SELECT 'abc FROM Customers" },
        new object[] { "SELECT 1.2.3 FROM t" },
        new object[] { "SELECT * FROM t WHERE x = 1." },
        new object[] { "SELECT [unterminated FROM t" },
        new object[] { "SELECT \"unterminated FROM t" },
        new object[] { "SELECT [] FROM t" },
        new object[] { "SELECT \"\" FROM t" },
        new object[] { "SELECT 1abc FROM t" },
    };

    public static IEnumerable<object[]> DmlErrors => new[]
    {
        new object[] { "INSERT INTO Customers (a, b,) VALUES (1, 2)" },
        new object[] { "INSERT INTO t VALUES" },
        new object[] { "INSERT INTO t VALUES ()" },
        new object[] { "INSERT INTO t VALUES (1,)" },
        new object[] { "INSERT INTO VALUES (1, 2)" },
        new object[] { "UPDATE" },
        new object[] { "UPDATE t SET" },
        new object[] { "UPDATE t SET = 1" },
        new object[] { "UPDATE t SET a =" },
        new object[] { "UPDATE t WHERE id = 1" },
        new object[] { "UPDATE Customers SET a = 1, b = 2, WHERE id = 1" },
    };

    public static IEnumerable<object[]> SubqueriesAndSets => new[]
    {
        new object[] { "SELECT * FROM (SELECT)" },
        new object[] { "SELECT * FROM ()" },
        new object[] { "SELECT (SELECT FROM t) FROM t" },
        new object[] { "SELECT a FROM t UNION" },
        new object[] { "SELECT a FROM t UNION ALL" },
        new object[] { "SELECT a FROM t INTERSECT" },
        new object[] { "SELECT a FROM t EXCEPT" },
        new object[] { "WITH" },
        new object[] { "WITH x AS" },
        new object[] { "WITH x AS () SELECT * FROM x" },
        new object[] { "WITH x AS (SELECT 1) SELECT FROM x" },
    };

    // ── Dialect-specific (SQL Server) ─────────────────────────────────────────
    public static IEnumerable<object[]> DialectSpecific => new[]
    {
        new object[] { "SELECT TOP FROM Customers" }, // bare TOP
    };

    [Theory]
    [MemberData(nameof(StatementLevel))]
    [MemberData(nameof(SelectClause))]
    [MemberData(nameof(FromAndJoin))]
    [MemberData(nameof(Expressions))]
    [MemberData(nameof(IdentifiersAndLiterals))]
    [MemberData(nameof(DmlErrors))]
    [MemberData(nameof(SubqueriesAndSets))]
    [MemberData(nameof(DialectSpecific))]
    public void MalformedSql_ProducesParseError(string sql)
    {
        TestGrammar grammar = new();
        ParseTree parseTree = GrammarParser.ParseTree(grammar, sql);

        Assert.True(parseTree.HasErrors(), $"Expected parser to reject: {sql}");
        Assert.NotEmpty(parseTree.ParserMessages);
        Assert.All(parseTree.ParserMessages, msg => Assert.False(string.IsNullOrWhiteSpace(msg.Message)));
    }
}
