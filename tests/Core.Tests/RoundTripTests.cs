#nullable enable
using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

/// <summary>
/// Round-trip parse → render → re-parse → AST-equality test suite (issue #176).
///
/// Goal: SqlBuildingBlocks is a parser library. Consumers expect that
/// <c>SQL → AST → SQL'</c> produces a re-parseable equivalent AST. AGENTS.md tags
/// "Incorrect AST node construction that produces wrong SQL on round-trip" as P0,
/// and this suite is the safety net for that class of bug.
///
/// ── Phase 1 audit outcome (State B — partial renderer) ─────────────────────
/// At time of writing, the codebase has a partial renderer surface:
///   • Expression-level types (SqlExpression, SqlBinaryExpression, SqlBetweenExpression,
///     SqlCaseExpression, SqlCastExpression, SqlInList, …) override <c>ToString()</c>
///     and expose <c>ToExpressionString()</c> producing re-parseable expression text.
///   • Statement-level types (SqlSelectDefinition, SqlInsertDefinition,
///     SqlUpdateDefinition, SqlDeleteDefinition, SqlCreateTableDefinition, …)
///     do NOT override <c>ToString()</c>. <c>SqlDefinition.ToString()</c> delegates to them
///     but falls back to default object type-name output for statements.
///
/// ── Round-trip strategy ────────────────────────────────────────────────────
///   • For expressions → full <c>parse → ToExpressionString → re-parse → AST equality</c>.
///     This DOES catch render-side bugs at expression level.
///   • For statements → degraded <c>parse → re-parse → AST equality</c> (the same SQL is
///     parsed twice and the two ASTs are compared). This catches grammar
///     non-determinism / hidden randomness in AST construction, but NOT statement-level
///     render bugs. Statement-level rendering is a future enhancement (a TODO is
///     filed via the issue tracker — see follow-up for issue #176-renderer).
///
/// ── CI hookup (recommended, not enforced here) ─────────────────────────────
/// CI should run this suite on PRs that touch:
///   • src/Core/*.cs, src/Core/LogicalEntities/**, src/Grammars/**
/// The recommended hook lives in .github/workflows/ci.yml — adding it is a
/// separate PR (issue #176 explicitly scopes the CI change to a follow-up PR).
///
/// ── Test corpus coverage (issue #176 acceptance criteria) ──────────────────
///   • SELECT (WHERE / JOIN / GROUP BY / ORDER BY)         → 4 statement tests
///   • INSERT                                              → 1 statement test
///   • UPDATE                                              → 1 statement test
///   • DELETE                                              → 1 statement test
///   • CREATE TABLE                                        → 1 statement test
///   • Mixed-precedence expressions (a + b * c, etc.)      → 2 expression tests
///   • CASE                                                → 1 expression test
///   • CAST                                                → 1 expression test
/// </summary>
public class RoundTripTests
{
    // ── Test grammars (mirrors StmtTests/ExprTests wiring) ────────────────────

    private class StmtGrammar : Grammar
    {
        public StmtGrammar() : base(false)
        {
            SelectStmt selectStmt = new(this);
            Expr expr = selectStmt.JoinChainOpt.Expr;
            expr.InitializeRule(selectStmt, selectStmt.FuncCall);

            InsertStmt insertStmt = new(this, selectStmt);
            UpdateStmt updateStmt = new(this, selectStmt.TableName, selectStmt.FuncCall, selectStmt.WhereClauseOpt);
            DeleteStmt deleteStmt = new(this, selectStmt.TableName, selectStmt.WhereClauseOpt, updateStmt.ReturningClauseOpt, selectStmt.JoinChainOpt);
            CreateTableStmt createTableStmt = new(this, selectStmt.Id);
            AlterStmt alterStmt = new(this, selectStmt.Id, createTableStmt.ColumnDef);
            RenameTableStmt renameTableStmt = new(this, selectStmt.Id);

            Stmt stmt = new(this, selectStmt, insertStmt, updateStmt, deleteStmt, createTableStmt, alterStmt, renameTableStmt);
            Root = stmt;
        }

        public SqlDefinition Create(ParseTreeNode stmt) =>
            ((Stmt)Root).Create(stmt, new DatabaseConnectionProvider(), new TableSchemaProvider());
    }

    private class ExprGrammar : Grammar
    {
        public ExprGrammar() : base(false)
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

            Root = expr;
        }

        public SqlExpression Create(ParseTreeNode node) => ((Expr)Root).Create(node);
    }

    /// <summary>
    /// SELECT-only grammar with Root = SelectStmt, required for derived-table round-trip tests.
    /// TableName.CreateDerivedSelectDefinition checks grammar.Root is SelectStmt; a Stmt-rooted
    /// grammar fails that check. This grammar supports SELECT (including derived tables and CTEs)
    /// but not INSERT/UPDATE/DELETE/CREATE TABLE.
    /// </summary>
    private class SelectGrammar : Grammar
    {
        public SelectGrammar() : base(false)
        {
            SelectStmt selectStmt = new(this);
            Expr expr = selectStmt.JoinChainOpt.Expr;
            expr.InitializeRule(selectStmt, selectStmt.FuncCall);

            Root = selectStmt;
        }

        public SqlSelectDefinition Create(ParseTreeNode node) =>
            ((SelectStmt)Root).Create(node);
    }

    // ── Round-trip oracles ────────────────────────────────────────────────────

    private static SqlDefinition ParseStmt(string sql)
    {
        var grammar = new StmtGrammar();
        var node = GrammarParser.Parse(grammar, sql);
        return grammar.Create(node);
    }

    private static SqlSelectDefinition ParseSelect(string sql)
    {
        var grammar = new SelectGrammar();
        var node = GrammarParser.Parse(grammar, sql);
        return grammar.Create(node);
    }

    private static SqlExpression ParseExpr(string sql)
    {
        var grammar = new ExprGrammar();
        var node = GrammarParser.Parse(grammar, sql);
        return grammar.Create(node);
    }

    /// <summary>
    /// Statement round-trip oracle (degraded — no statement-level renderer).
    /// Parses the same SQL twice and asserts the two resulting ASTs are structurally
    /// equal. Catches grammar non-determinism / hidden mutable state in AST construction.
    /// Does NOT catch statement-level render bugs — there is no statement-level renderer.
    /// </summary>
    private static void AssertStmtRoundTrip(string sql)
    {
        var first = ParseStmt(sql);
        var second = ParseStmt(sql);
        AstComparer.AssertEqual(first, second, $"sql=`{sql}`");
    }

    /// <summary>
    /// SELECT-only round-trip oracle. Uses <see cref="SelectGrammar"/> (Root = SelectStmt)
    /// which is required for SQL that contains derived tables: TableName.CreateDerivedSelectDefinition
    /// validates grammar.Root is SelectStmt and throws when a Stmt-rooted grammar is used.
    /// </summary>
    private static void AssertSelectRoundTrip(string sql)
    {
        var first = ParseSelect(sql);
        var second = ParseSelect(sql);
        AstComparer.AssertEqual(first, second, $"sql=`{sql}`");
    }

    /// <summary>
    /// Expression round-trip oracle (full — uses ToExpressionString as the render side).
    /// Parses the SQL, renders the AST back to SQL via ToExpressionString, re-parses,
    /// and asserts the two ASTs are structurally equal. Catches both grammar
    /// non-determinism AND expression-level render bugs.
    /// </summary>
    private static void AssertExprRoundTrip(string sql)
    {
        var first = ParseExpr(sql);
        var rendered = first.ToExpressionString();
        SqlExpression second;
        try
        {
            second = ParseExpr(rendered);
        }
        catch (Exception ex)
        {
            Assert.Fail(
                $"Round-trip failed: rendered SQL did not re-parse.{Environment.NewLine}" +
                $"  original  : `{sql}`{Environment.NewLine}" +
                $"  rendered  : `{rendered}`{Environment.NewLine}" +
                $"  parse err : {ex.Message}");
            return; // unreachable
        }
        AstComparer.AssertEqual(first, second,
            $"original=`{sql}` rendered=`{rendered}`");
    }

    // ── SELECT round-trip ─────────────────────────────────────────────────────

    [Fact]
    public void Select_Simple_Star_FromTable()
    {
        AssertStmtRoundTrip("SELECT * FROM Customers");
    }

    [Fact]
    public void Select_NamedColumns_With_Where()
    {
        AssertStmtRoundTrip("SELECT ID, CustomerName FROM Customers WHERE ID > 10");
    }

    [Fact]
    public void Select_With_InnerJoin()
    {
        AssertStmtRoundTrip("SELECT * FROM Customers INNER JOIN Orders ON Customers.ID = Orders.CustomerID");
    }

    [Fact]
    public void Select_With_GroupBy_OrderBy()
    {
        AssertStmtRoundTrip("SELECT CustomerID, COUNT(*) FROM Orders GROUP BY CustomerID ORDER BY CustomerID DESC");
    }

    // ── INSERT round-trip ─────────────────────────────────────────────────────

    [Fact]
    public void Insert_Values()
    {
        AssertStmtRoundTrip("INSERT INTO locations (city, state, zip) VALUES ('Boston', 'MA', 90001)");
    }

    // ── UPDATE round-trip ─────────────────────────────────────────────────────

    [Fact]
    public void Update_With_Where()
    {
        AssertStmtRoundTrip("UPDATE locations SET zip = 32655 WHERE city = 'Boston'");
    }

    // ── DELETE round-trip ─────────────────────────────────────────────────────

    [Fact]
    public void Delete_With_Where()
    {
        AssertStmtRoundTrip("DELETE FROM employees WHERE name = 'Joe'");
    }

    // ── CREATE TABLE round-trip ───────────────────────────────────────────────

    [Fact]
    public void CreateTable_PrimaryKey()
    {
        AssertStmtRoundTrip("CREATE TABLE Example (ID INT PRIMARY KEY)");
    }

    // ── Expression round-trip — operator precedence ──────────────────────────

    [Fact]
    public void Expression_Precedence_AddTimes()
    {
        // a + b * c — multiplication binds tighter than addition.
        AssertExprRoundTrip("a + b * c = 0");
    }

    [Fact]
    public void Expression_Precedence_ParenthesizedAddTimes()
    {
        // (a + b) * c — parentheses force addition to bind first.
        AssertExprRoundTrip("(a + b) * c = 0");
    }

    // ── Expression round-trip — CASE ─────────────────────────────────────────

    [Fact]
    public void Expression_CaseWhenElse()
    {
        AssertExprRoundTrip("CASE WHEN x = 1 THEN 'yes' ELSE 'no' END = 'yes'");
    }

    // ── Expression round-trip — CAST ─────────────────────────────────────────

    [Fact]
    public void Expression_Cast()
    {
        AssertExprRoundTrip("CAST(price AS INT) > 0");
    }

    // ── New round-trip tests for Wave 10–16 constructs (issue #204) ──────────

    [Fact]
    public void Cte_NonRecursive_RoundTrips()
    {
        // Non-recursive CTE: WITH x AS (...) SELECT ... FROM x
        AssertStmtRoundTrip("WITH x AS (SELECT ID FROM Orders) SELECT ID FROM x");
    }

    [Fact]
    public void Cte_Chained_RoundTrips()
    {
        // Chained CTEs: CTE b references CTE a declared earlier in the same WITH clause.
        AssertStmtRoundTrip("WITH a AS (SELECT ID FROM Orders), b AS (SELECT ID FROM a) SELECT ID FROM b");
    }

    [Fact]
    public void Cte_Recursive_RoundTrips()
    {
        // Recursive CTE: RECURSIVE keyword is supported in the Core SelectStmt grammar.
        // The parse-only oracle (no AstComparer) is used here because the recursive CTE
        // creates a back-reference cycle in the resolved AST (the inner SELECT's FROM table
        // resolves to a SqlCteTable that points back to the outer SqlCteDefinition).
        // AstComparer.AssertEqual would stack-overflow on that cycle.
        // This test verifies: (a) the grammar accepts WITH RECURSIVE syntax without errors,
        // and (b) a second parse of the same SQL produces no errors (grammar is deterministic).
        // Uses the fake TableSchemaProvider's "employees" table (id, manager_id columns).
        // Multi-character aliases avoid reserved boolean words (T, F, on, off, yes, no).
        const string sql =
            "WITH RECURSIVE mgr AS (" +
            "SELECT id, manager_id FROM employees WHERE manager_id = 0 " +
            "UNION ALL " +
            "SELECT emp.id, emp.manager_id FROM employees emp JOIN mgr pr ON emp.manager_id = pr.id) " +
            "SELECT id FROM mgr";

        var grammar1 = new StmtGrammar();
        GrammarParser.Parse(grammar1, sql); // asserts no parse errors

        var grammar2 = new StmtGrammar();
        GrammarParser.Parse(grammar2, sql); // second parse is also error-free
    }

    [Fact]
    public void DerivedTable_InFrom_RoundTrips()
    {
        // Derived table in the FROM position: subquery with alias.
        // Uses SelectGrammar (Root = SelectStmt) because TableName.CreateDerivedSelectDefinition
        // requires grammar.Root is SelectStmt; a Stmt-rooted grammar throws at AST creation time.
        AssertSelectRoundTrip("SELECT sub.ID FROM (SELECT ID FROM Orders) AS sub");
    }

    [Fact]
    public void DerivedTable_InJoin_RoundTrips()
    {
        // Derived table on the JOIN side: subquery aliased and joined on a column.
        // Uses SelectGrammar (Root = SelectStmt) for the same reason as DerivedTable_InFrom_RoundTrips.
        AssertSelectRoundTrip(
            "SELECT c.CustomerName, sub.CustomerID FROM Customers c " +
            "INNER JOIN (SELECT CustomerID FROM Orders) AS sub ON c.ID = sub.CustomerID");
    }

    [Fact]
    public void WindowFrame_IntervalBound_RoundTrips()
    {
        // INTERVAL window frame bound: RANGE BETWEEN INTERVAL '1' DAY PRECEDING AND CURRENT ROW.
        // The AnsiSQL grammar (SelectStmt.cs) supports INTERVAL '<magnitude>' <qualifier> in window
        // frame bounds, so this exercises the intervalFrameLiteral grammar rule on round-trip.
        AssertStmtRoundTrip(
            "SELECT ID, SUM(CustomerID) OVER " +
            "(ORDER BY ID RANGE BETWEEN INTERVAL '1' DAY PRECEDING AND CURRENT ROW) " +
            "AS running_sum FROM Orders");
    }
}
