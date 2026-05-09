using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using Xunit;

namespace SqlBuildingBlocks.Grammars.AnsiSQL.Tests;

/// <summary>
/// Negative / malformed-SQL parsing tests for the AnsiSQL grammar.
///
/// Per AGENTS.md P0 priorities, the parser must not silently accept malformed SQL.
/// Each test feeds a known-bad SQL string and asserts that the parse tree reports
/// errors (HasErrors() == true) AND that the parser produced a non-empty error
/// message. The actual message text is not asserted because the Irony parser's
/// wording is not stable API.
///
/// Issue #175 (Wave 9, 2026-05-09): expanded from the Wave 2 baseline (~10 cases) and
/// the Wave 6 / #167 dangling-comma addition to ≥50 cases per dialect, organised by
/// category so that future grammar additions can be slotted into the right bucket.
/// The shared audit doc lives at <c>Docs/Audit/silent-accept-corpus-2026.md</c>.
///
/// Inputs that the grammar happens to accept (rather than reject) are intentionally
/// not in this catalog — those are findings rather than enforced behavior. The
/// only intentional non-rejection found by the audit is bare <c>SELECT *</c> with
/// no FROM clause, which is well-formed per the grammar (the FROM clause is
/// optional, e.g. <c>SELECT 1</c>, <c>SELECT current_timestamp</c>).
/// </summary>
public class NegativeTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar() : base(false) // SQL is case insensitive
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
            AnsiSQL.SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);
            expr.InitializeRule(selectStmt, funcCall);

            InsertStmt insertStmt = new(this, id, expr, selectStmt);
            UpdateStmt updateStmt = new(this, id, literalValue, parameter, funcCall, tableName, whereClauseOpt, joinChainOpt);

            Stmt stmt = new(this, selectStmt, insertStmt, updateStmt);
            Root = stmt;
        }
    }

    // ── Statement-level malformation ──────────────────────────────────────────
    public static IEnumerable<object[]> StatementLevel => new[]
    {
        new object[] { "" },                              // empty input
        new object[] { "   " },                           // whitespace-only
        new object[] { ";" },                             // bare semicolon
        new object[] { "-- nothing" },                    // bare comment
        new object[] { "FROM Customers" },                // reserved word as stmt start
        new object[] { "WHERE x = 1" },                   // reserved word as stmt start
        new object[] { "SELECT 1; SELECT 2" },            // multiple statements (Stmt is single)
    };

    // ── SELECT clause errors ──────────────────────────────────────────────────
    public static IEnumerable<object[]> SelectClause => new[]
    {
        new object[] { "SELECT FROM t" },                 // empty SELECT list
        new object[] { "SELECT , FROM t" },               // bare comma in select
        new object[] { "SELECT *, x FROM t" },            // star + column (ANSI rejects)
        new object[] { "SELECT x, * FROM t" },            // column + star
        new object[] { "SELECT DISTINCT ALL x FROM t" },
        new object[] { "SELECT ALL DISTINCT x FROM t" },
        new object[] { "SELECT a FROM t GROUP BY" },      // bare GROUP BY
        new object[] { "SELECT a FROM t GROUP BY a," },   // dangling GROUP BY comma
        new object[] { "SELECT a FROM t ORDER BY" },      // bare ORDER BY
        new object[] { "SELECT a FROM t ORDER BY a," },   // dangling ORDER BY comma
        new object[] { "SELECT a FROM t HAVING" },        // bare HAVING
        new object[] { "SELECT a FROM t WHERE x = 1 GROUP BY a HAVING" },
        new object[] { "SELECT a, FROM Customers" },      // dangling SELECT comma (#167)
        new object[] { "SELECT * FROM Customers WHERE" }, // bare WHERE
        new object[] { "SELECT * Customers" },            // missing FROM
    };

    // ── FROM / JOIN clause errors ─────────────────────────────────────────────
    public static IEnumerable<object[]> FromAndJoin => new[]
    {
        new object[] { "SELECT * FROM" },                 // bare FROM
        new object[] { "SELECT * FROM a, b," },           // trailing comma in FROM
        new object[] { "SELECT * FROM a, , b" },          // bare middle comma in FROM
        new object[] { "SELECT * FROM a JOIN b ON" },     // ON with no predicate
        new object[] { "SELECT * FROM a JOIN b ON x = " },// ON with trailing operator
        new object[] { "SELECT * FROM a INNER JOIN" },    // INNER JOIN with no table or condition
        new object[] { "SELECT * FROM a LEFT JOIN" },
        new object[] { "SELECT * FROM a RIGHT JOIN" },
        new object[] { "SELECT * FROM a FULL JOIN b" },   // missing ON
    };

    // ── Expression errors ─────────────────────────────────────────────────────
    public static IEnumerable<object[]> Expressions => new[]
    {
        new object[] { "SELECT (1 + 2 FROM Customers" },          // unclosed parens
        new object[] { "SELECT * FROM Customers WHERE x IN ()" }, // empty IN list
        new object[] { "SELECT * FROM t WHERE x BETWEEN 1 AND" }, // BETWEEN no RHS
        new object[] { "SELECT * FROM t WHERE x BETWEEN AND 5" }, // BETWEEN no LHS
        new object[] { "SELECT * FROM t WHERE x BETWEEN 1" },     // BETWEEN no AND
        new object[] { "SELECT * FROM t WHERE x IS NULL NULL" },  // IS NULL NULL
        new object[] { "SELECT * FROM t WHERE x IS" },            // IS with nothing
        new object[] { "SELECT * FROM t WHERE CASE END" },        // CASE no WHEN
        new object[] { "SELECT * FROM t WHERE CASE WHEN x THEN y" },// CASE no END
        new object[] { "SELECT * FROM t WHERE CASE WHEN x END" }, // CASE WHEN no THEN
        new object[] { "SELECT * FROM t WHERE MAX(x" },           // unclosed func call
        new object[] { "SELECT * FROM t WHERE MAX x)" },          // missing open paren
        new object[] { "SELECT * FROM Customers WHERE x =" },     // trailing operator
        new object[] { "SELECT * FROM t WHERE = 1" },             // lone operator
        new object[] { "SELECT * FROM t WHERE x LIKE" },          // LIKE no pattern
        new object[] { "SELECT * FROM t WHERE x IN (,)" },        // IN with bare comma
        new object[] { "SELECT * FROM t WHERE x IN (1,)" },       // IN trailing comma
        new object[] { "SELECT * FROM t WHERE x IN (,1)" },       // IN leading comma
        new object[] { "SELECT * FROM t WHERE x AND" },           // bare AND
        new object[] { "SELECT * FROM t WHERE AND x = 1" },       // AND with no LHS
        new object[] { "SELECT * FROM t WHERE x = 1 OR" },        // OR no RHS
        new object[] { "SELECT * FROM t WHERE NOT" },             // bare NOT
        new object[] { "SELECT * FROM t WHERE EXISTS" },          // bare EXISTS
        new object[] { "SELECT * FROM t WHERE EXISTS ()" },       // EXISTS empty parens
    };

    // ── Identifier / literal errors ───────────────────────────────────────────
    public static IEnumerable<object[]> IdentifiersAndLiterals => new[]
    {
        new object[] { "SELECT 'abc FROM Customers" },    // unterminated string
        new object[] { "SELECT 1.2.3 FROM t" },           // multi-dot numeric
        new object[] { "SELECT * FROM t WHERE x = 1." },  // trailing dot numeric
        new object[] { "SELECT [unterminated FROM t" },   // unterminated bracket
        new object[] { "SELECT \"unterminated FROM t" }, // unterminated quoted id
        new object[] { "SELECT [] FROM t" },              // empty brackets
        new object[] { "SELECT \"\" FROM t" },           // empty quoted id
        new object[] { "SELECT 1abc FROM t" },            // numeric identifier prefix
    };

    // ── DML errors (INSERT/UPDATE/DELETE) ─────────────────────────────────────
    public static IEnumerable<object[]> DmlErrors => new[]
    {
        new object[] { "INSERT INTO Customers (a, b,) VALUES (1, 2)" }, // dangling INSERT col comma
        new object[] { "INSERT INTO t VALUES" },          // VALUES nothing
        new object[] { "INSERT INTO t VALUES ()" },       // empty VALUES tuple
        new object[] { "INSERT INTO t VALUES (1,)" },     // trailing comma in VALUES
        new object[] { "INSERT INTO VALUES (1, 2)" },     // missing table
        new object[] { "UPDATE" },                        // bare UPDATE
        new object[] { "UPDATE t SET" },                  // SET nothing
        new object[] { "UPDATE t SET = 1" },              // SET no LHS
        new object[] { "UPDATE t SET a =" },              // SET no RHS
        new object[] { "UPDATE t WHERE id = 1" },         // missing SET clause
        new object[] { "UPDATE Customers SET a = 1, b = 2, WHERE id = 1" }, // dangling SET comma
    };

    // ── Sub-queries & set operations ──────────────────────────────────────────
    public static IEnumerable<object[]> SubqueriesAndSets => new[]
    {
        new object[] { "SELECT * FROM (SELECT)" },        // empty subquery
        new object[] { "SELECT * FROM ()" },              // empty paren table source
        new object[] { "SELECT (SELECT FROM t) FROM t" }, // empty inner SELECT
        new object[] { "SELECT a FROM t UNION" },         // UNION nothing
        new object[] { "SELECT a FROM t UNION ALL" },     // UNION ALL nothing
        new object[] { "SELECT a FROM t INTERSECT" },     // INTERSECT nothing
        new object[] { "SELECT a FROM t EXCEPT" },        // EXCEPT nothing
        new object[] { "WITH" },                          // bare WITH
        new object[] { "WITH x AS" },                     // WITH no body
        new object[] { "WITH x AS () SELECT * FROM x" },  // empty CTE body
        new object[] { "WITH x AS (SELECT 1) SELECT FROM x" }, // CTE then bad outer
    };

    // ── Dialect-specific (AnsiSQL) ────────────────────────────────────────────
    public static IEnumerable<object[]> DialectSpecific => new[]
    {
        new object[] { "SELECT * FROM Customers FETCH FIRST" }, // malformed FETCH FIRST
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
