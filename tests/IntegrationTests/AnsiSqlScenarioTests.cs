using SqlBuildingBlocks.IntegrationTests.Infrastructure;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.QueryProcessing;
using SqlBuildingBlocks.Utils;
using System.Data;
using Xunit;

namespace SqlBuildingBlocks.IntegrationTests;

/// <summary>
/// End-to-end scenarios that validate the AnsiSQL grammar drives the QueryEngine
/// against a synthetic in-memory database. Each test exercises the full stack:
/// grammar -> parser -> logical entities -> query engine -> result set.
/// </summary>
public class AnsiSqlScenarioTests
{
    private static InMemoryDatabase BuildSampleDatabase()
    {
        var db = new InMemoryDatabase("Sales");

        var customers = db.AddTable("Customers",
            ("ID", typeof(int)),
            ("CustomerName", typeof(string)),
            ("Region", typeof(string)));
        customers.Rows.Add(1, "Alice", "North");
        customers.Rows.Add(2, "Bob", "South");
        customers.Rows.Add(3, "Carol", "North");
        customers.Rows.Add(4, "Dan", "East");

        var orders = db.AddTable("Orders",
            ("ID", typeof(int)),
            ("CustomerID", typeof(int)),
            ("Amount", typeof(decimal)));
        orders.Rows.Add(100, 1, 50.00m);
        orders.Rows.Add(101, 1, 25.00m);
        orders.Rows.Add(102, 2, 75.00m);
        orders.Rows.Add(103, 3, 10.00m);
        orders.Rows.Add(104, 3, 30.00m);
        orders.Rows.Add(105, 3, 40.00m);

        return db;
    }

    private static DataTable Run(string sql, InMemoryDatabase db)
    {
        var grammar = new AnsiSqlGrammar();
        var node = ParseHelper.Parse(grammar, sql);
        var selectDefinition = grammar.CreateSelect(node, db, db);
        Assert.False(selectDefinition.InvalidReferences,
            $"Reference resolution failed for SQL: {sql}");

        var allTableProvider = new AllTableDataProvider(new[] { (SqlBuildingBlocks.Interfaces.ITableDataProvider)db });
        var engine = new QueryEngine(allTableProvider, selectDefinition);
        return engine.QueryAsDataTable();
    }

    [Fact]
    public void Scenario_BasicProjectionAndWhere()
    {
        // Scenario 1: AnsiSQL SELECT — basic projection + WHERE on Customers.
        var db = BuildSampleDatabase();

        var result = Run("SELECT ID, CustomerName FROM Customers WHERE Region = 'North'", db);

        Assert.Equal(2, result.Columns.Count);
        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows.Cast<DataRow>(), r => (string)r["CustomerName"] == "Alice");
        Assert.Contains(result.Rows.Cast<DataRow>(), r => (string)r["CustomerName"] == "Carol");
    }

    [Fact]
    public void Scenario_InnerJoin_CustomersAndOrders()
    {
        // Scenario 2: AnsiSQL JOIN — Customers x Orders.
        var db = BuildSampleDatabase();

        var result = Run(
            "SELECT c.CustomerName, o.Amount FROM Customers c INNER JOIN Orders o ON c.ID = o.CustomerID",
            db);

        Assert.Equal(2, result.Columns.Count);
        Assert.Equal(6, result.Rows.Count); // 2 for Alice + 1 for Bob + 3 for Carol = 6
        var aliceCount = result.Rows.Cast<DataRow>().Count(r => (string)r["CustomerName"] == "Alice");
        var carolCount = result.Rows.Cast<DataRow>().Count(r => (string)r["CustomerName"] == "Carol");
        Assert.Equal(2, aliceCount);
        Assert.Equal(3, carolCount);
    }

    [Fact]
    public void Scenario_AggregateGroupBy_OrdersPerCustomer()
    {
        // Scenario 3: aggregate + GROUP BY — orders per customer.
        var db = BuildSampleDatabase();

        var result = Run("SELECT CustomerID, COUNT(*) FROM Orders GROUP BY CustomerID", db);

        Assert.Equal(2, result.Columns.Count);
        Assert.Equal(3, result.Rows.Count); // 3 distinct customers (1, 2, 3)
        var byCustomer = result.Rows.Cast<DataRow>()
            .ToDictionary(r => Convert.ToInt32(r[0]), r => Convert.ToInt32(r[1]));
        Assert.Equal(2, byCustomer[1]);
        Assert.Equal(1, byCustomer[2]);
        Assert.Equal(3, byCustomer[3]);
    }

    [Fact]
    public void Scenario_OrderByDescending_TopByAmount()
    {
        // Scenario 4: ORDER BY — orders sorted by amount descending.
        var db = BuildSampleDatabase();

        var result = Run("SELECT ID, Amount FROM Orders ORDER BY Amount DESC", db);

        Assert.Equal(6, result.Rows.Count);
        // Order should be 75, 50, 40, 30, 25, 10
        Assert.Equal(75.00m, (decimal)result.Rows[0]["Amount"]);
        Assert.Equal(50.00m, (decimal)result.Rows[1]["Amount"]);
        Assert.Equal(10.00m, (decimal)result.Rows[5]["Amount"]);
    }

    [Fact]
    public void Scenario_NonRecursiveCte_ParsesToSqlSelectDefinition()
    {
        // Scenario 5a: AnsiSQL CTE (non-recursive) — parse-side integration.
        // The grammar parses WITH ... AS (...) into SqlSelectDefinition.Ctes correctly.
        // Wave 10 (#174) made SelectReferenceResolver CTE-aware, so reference resolution
        // now succeeds when the CTE name is supplied via WITH; pass db, db so this test
        // exercises the full parse + resolve pipeline.
        var db = BuildSampleDatabase();
        var grammar = new AnsiSqlGrammar();
        var node = ParseHelper.Parse(grammar,
            "WITH AllOrders AS (SELECT ID, Amount FROM Orders) SELECT ID FROM AllOrders");
        var selectDefinition = grammar.CreateSelect(node, db, db);

        Assert.False(selectDefinition.InvalidReferences,
            $"Reference resolution failed: {selectDefinition.InvalidReferenceReason}");
        Assert.Single(selectDefinition.Ctes);
        Assert.Equal("AllOrders", selectDefinition.Ctes[0].Name);
        Assert.NotNull(selectDefinition.Ctes[0].SelectDefinition);
        Assert.Equal("Orders", selectDefinition.Ctes[0].SelectDefinition!.Table!.TableName);
        // Main SELECT's FROM table is rewritten to a SqlCteTable carrying the CTE projection.
        Assert.IsType<SqlCteTable>(selectDefinition.Table);
        Assert.Equal("AllOrders", selectDefinition.Table!.TableName);
    }

    [Fact]
    public void Scenario_NonRecursiveCte_ExecutesEndToEnd()
    {
        // Scenario 5b: end-to-end execution of a parsed (rather than hand-built) non-recursive CTE.
        // Wave 10 (#174) — SelectReferenceResolver now pre-resolves each CTE and surfaces it as a
        // SqlCteTable so QueryEngine.ExecuteWithCtes can run the main SELECT against the materialized
        // CTE result. There are 6 rows in Orders so AllOrders projects 6 IDs.
        var db = BuildSampleDatabase();

        var result = Run(
            "WITH AllOrders AS (SELECT ID, Amount FROM Orders) SELECT ID FROM AllOrders",
            db);

        Assert.Single(result.Columns);
        Assert.Equal(6, result.Rows.Count);
        var ids = result.Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r["ID"])).OrderBy(i => i).ToList();
        Assert.Equal(new[] { 100, 101, 102, 103, 104, 105 }, ids);
    }

    [Fact]
    public void Scenario_ChainedCtes_BReferencesA()
    {
        // Scenario 5c: chained CTEs — CTE b references CTE a declared earlier in the same WITH.
        // Validates that prior CTEs are surfaced into the next CTE's outerTablesInScope.
        var db = BuildSampleDatabase();

        var result = Run(
            "WITH a AS (SELECT ID, Amount FROM Orders), b AS (SELECT ID FROM a) SELECT ID FROM b",
            db);

        Assert.Single(result.Columns);
        Assert.Equal(6, result.Rows.Count);
    }

    [Fact]
    public void Scenario_TwoCtesJoined_ExecutesEndToEnd()
    {
        // Scenario 5d: two distinct CTEs used in the main query — one as FROM, one as JOIN.
        // Each rewrites independently so the JOIN evaluates between the two CTE projections.
        var db = BuildSampleDatabase();

        var result = Run(
            "WITH OrderInfo AS (SELECT ID, CustomerID FROM Orders), " +
            "     CustomerInfo AS (SELECT ID, CustomerName FROM Customers) " +
            "SELECT CustomerInfo.CustomerName, OrderInfo.ID " +
            "FROM OrderInfo INNER JOIN CustomerInfo ON OrderInfo.CustomerID = CustomerInfo.ID",
            db);

        Assert.Equal(2, result.Columns.Count);
        // 6 orders, all customer IDs (1, 2, 3) exist → 6 joined rows.
        Assert.Equal(6, result.Rows.Count);
    }

    [Fact]
    public void Scenario_CteWithWhereInBody_ExecutesEndToEnd()
    {
        // Scenario 5e: CTE body has a non-trivial WHERE clause that filters Orders before the main SELECT.
        // CustomerID = 1 has 2 orders (IDs 100, 101).
        var db = BuildSampleDatabase();

        var result = Run(
            "WITH BigOrders AS (SELECT ID, Amount FROM Orders WHERE Amount > 25) SELECT ID FROM BigOrders",
            db);

        // Orders > 25 are: 50 (ID 100), 75 (ID 102), 30 (ID 104), 40 (ID 105) → 4 rows.
        Assert.Equal(4, result.Rows.Count);
        var ids = result.Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r["ID"])).OrderBy(i => i).ToList();
        Assert.Equal(new[] { 100, 102, 104, 105 }, ids);
    }

    [Fact]
    public void Scenario_CteWithJoinInBody_ExecutesEndToEnd()
    {
        // Scenario 5f: CTE body has a JOIN. The CTE itself becomes a flat projection over
        // the joined result, then the main SELECT projects from the CTE.
        var db = BuildSampleDatabase();

        var result = Run(
            "WITH CustomerOrders AS (" +
            "  SELECT c.CustomerName, o.Amount FROM Customers c INNER JOIN Orders o ON c.ID = o.CustomerID" +
            ") SELECT CustomerName, Amount FROM CustomerOrders",
            db);

        Assert.Equal(2, result.Columns.Count);
        Assert.Equal(6, result.Rows.Count); // same row count as the inner JOIN
    }

    [Fact]
    public void Scenario_CteJoinedWithBaseTable_ExecutesEndToEnd()
    {
        // Scenario 5g: CTE is one side of a JOIN against a regular database table.
        // Validates that JOIN-side rewriting works (not just FROM-side).
        var db = BuildSampleDatabase();

        var result = Run(
            "WITH Big AS (SELECT ID, CustomerID FROM Orders WHERE Amount > 25) " +
            "SELECT c.CustomerName, b.ID FROM Customers c INNER JOIN Big b ON c.ID = b.CustomerID",
            db);

        Assert.Equal(2, result.Columns.Count);
        // 4 big orders → 4 joined rows (each CustomerID maps to a Customer).
        Assert.Equal(4, result.Rows.Count);
    }

    [Fact]
    public void Scenario_BinaryExpression_CoversSqlExpressionKinds()
    {
        // Scenario 9: query whose WHERE clause exercises BinExpr/Value/Column arms of SqlExpression.
        // Wave 4's discriminated-union enforcement should remain intact end-to-end.
        // CustomerID 3 has Amounts {10, 30, 40} so > 20 AND = 3 yields 30 and 40.
        var db = BuildSampleDatabase();

        var result = Run(
            "SELECT ID, Amount FROM Orders WHERE Amount > 20 AND CustomerID = 3",
            db);

        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows.Cast<DataRow>(), r =>
        {
            Assert.True((decimal)r["Amount"] > 20m);
            // Indirectly validates that AND chained with a second equality predicate works.
        });
    }

    [Fact]
    public void Scenario_WindowFunction_RunningSum()
    {
        // Scenario 11: window function — ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW.
        var db = new InMemoryDatabase("Sales");
        var employees = db.AddTable("Employees",
            ("Name", typeof(string)),
            ("Salary", typeof(decimal)));
        employees.Rows.Add("Alice", 50000m);
        employees.Rows.Add("Bob", 60000m);
        employees.Rows.Add("Carol", 70000m);

        var result = Run(
            "SELECT Name, Salary, SUM(Salary) OVER (ORDER BY Salary ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS running_sum FROM Employees",
            db);

        Assert.Equal(3, result.Rows.Count);
        // Result should expose a running_sum column.
        Assert.Contains(result.Columns.Cast<DataColumn>(), c => c.ColumnName == "running_sum");
        // Alice: 50000, Bob: 110000, Carol: 180000 (sorted by Salary asc)
        var rows = result.Rows.Cast<DataRow>().ToList();
        Assert.Equal(50000m, (decimal)rows[0]["running_sum"]);
        Assert.Equal(110000m, (decimal)rows[1]["running_sum"]);
        Assert.Equal(180000m, (decimal)rows[2]["running_sum"]);
    }

    [Fact]
    public void Scenario_DecimalLiteralComparison_FiltersByPriceGreaterThan()
    {
        // Issue #184: SQL `WHERE Amount > 25.00` previously failed because the grammar produced
        // a Double for the unsuffixed fractional literal but LiteralValue.Create only accepted
        // string and int. With the grammar now producing a Decimal for unsuffixed fractional
        // literals (DefaultFloatType = TypeCode.Decimal), this end-to-end path works:
        // parse → resolve → execute → match against decimal-typed Amount column.
        var db = BuildSampleDatabase();

        var result = Run("SELECT ID, Amount FROM Orders WHERE Amount > 25.00", db);

        // Amounts > 25.00 are: 50, 75, 30, 40 → 4 rows (10 and 25 excluded).
        Assert.Equal(4, result.Rows.Count);
        Assert.All(result.Rows.Cast<DataRow>(), r => Assert.True((decimal)r["Amount"] > 25.00m));
    }

    [Fact]
    public void Scenario_MalformedSql_ReportsParseError()
    {
        // Scenario 12: negative integration test — malformed SQL fails parse with a clear error surface.
        var grammar = new AnsiSqlGrammar();
        var parseTree = ParseHelper.ParseTree(grammar, "SELECT FROM WHERE");

        Assert.True(parseTree.HasErrors(),
            "Malformed SQL should produce parse errors but did not.");
        Assert.NotEmpty(parseTree.ParserMessages);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Issue #187 — Non-recursive CTE bodies that use UNION/INTERSECT/EXCEPT
    // must have their right-hand SetOperation SELECTs resolved at resolution
    // time (previously only recursive-CTE recursive terms were resolved).
    // Without the fix, an end-to-end run starting from parsed SQL fails because
    // the right-side SELECT has no resolved column→table refs by the time the
    // QueryEngine tries to execute it.
    // ─────────────────────────────────────────────────────────────────────────

    private static InMemoryDatabase BuildPeopleDatabase()
    {
        var db = new InMemoryDatabase("People");

        var employees = db.AddTable("Employees",
            ("ID", typeof(int)),
            ("Name", typeof(string)));
        employees.Rows.Add(1, "Alice");
        employees.Rows.Add(2, "Bob");
        employees.Rows.Add(3, "Carol");

        var contractors = db.AddTable("Contractors",
            ("ID", typeof(int)),
            ("Name", typeof(string)));
        contractors.Rows.Add(10, "Bob");    // overlaps with Employees by Name
        contractors.Rows.Add(11, "Dan");
        contractors.Rows.Add(12, "Carol");  // overlaps with Employees by Name

        return db;
    }

    [Fact]
    public void Scenario_NonRecursiveCte_UnionAllBody_ResolvesAndExecutes()
    {
        // Issue #187: CTE body uses UNION ALL; right-hand SELECT must be resolved.
        var db = BuildPeopleDatabase();

        var result = Run(
            "WITH AllPeople AS (SELECT Name FROM Employees UNION ALL SELECT Name FROM Contractors) " +
            "SELECT Name FROM AllPeople",
            db);

        // 3 employees + 3 contractors = 6 rows, with duplicates kept (Bob, Carol appear twice).
        Assert.Equal(6, result.Rows.Count);
        var names = result.Rows.Cast<DataRow>().Select(r => (string)r["Name"]).ToList();
        Assert.Equal(2, names.Count(n => n == "Bob"));
        Assert.Equal(2, names.Count(n => n == "Carol"));
    }

    [Fact]
    public void Scenario_NonRecursiveCte_UnionBody_DeduplicatesAndExecutes()
    {
        // Issue #187: CTE body uses UNION (deduplicating).
        var db = BuildPeopleDatabase();

        var result = Run(
            "WITH AllPeople AS (SELECT Name FROM Employees UNION SELECT Name FROM Contractors) " +
            "SELECT Name FROM AllPeople",
            db);

        // {Alice, Bob, Carol} ∪ {Bob, Dan, Carol} = {Alice, Bob, Carol, Dan} → 4 rows.
        Assert.Equal(4, result.Rows.Count);
        var names = result.Rows.Cast<DataRow>().Select(r => (string)r["Name"]).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "Alice", "Bob", "Carol", "Dan" }, names);
    }

    [Fact]
    public void Scenario_NonRecursiveCte_IntersectBody_ResolvesAndExecutes()
    {
        // Issue #187: CTE body uses INTERSECT.
        var db = BuildPeopleDatabase();

        var result = Run(
            "WITH Common AS (SELECT Name FROM Employees INTERSECT SELECT Name FROM Contractors) " +
            "SELECT Name FROM Common",
            db);

        // {Alice, Bob, Carol} ∩ {Bob, Dan, Carol} = {Bob, Carol}.
        Assert.Equal(2, result.Rows.Count);
        var names = result.Rows.Cast<DataRow>().Select(r => (string)r["Name"]).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "Bob", "Carol" }, names);
    }

    [Fact]
    public void Scenario_NonRecursiveCte_ExceptBody_ResolvesAndExecutes()
    {
        // Issue #187: CTE body uses EXCEPT.
        var db = BuildPeopleDatabase();

        var result = Run(
            "WITH OnlyEmployees AS (SELECT Name FROM Employees EXCEPT SELECT Name FROM Contractors) " +
            "SELECT Name FROM OnlyEmployees",
            db);

        // {Alice, Bob, Carol} − {Bob, Dan, Carol} = {Alice}.
        Assert.Single(result.Rows);
        Assert.Equal("Alice", (string)result.Rows[0]["Name"]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Issue #182 — Derived-table outer-scope leak. ResolveDerivedTables must
    // pass the parent's outer scope (and the parent's siblings in FROM/JOIN)
    // into the derived table's inner ResolveReferences so a derived table can
    // reference a correlated outer column.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Scenario_CorrelatedDerivedTable_ReferencesOuterColumn()
    {
        // Issue #182: derived table's WHERE clause references an outer column (c.ID).
        // Without the fix, the inner SELECT resolves with no outer scope and reports
        // c.ID as not found.
        var db = BuildSampleDatabase();
        var grammar = new AnsiSqlGrammar();

        var node = ParseHelper.Parse(grammar,
            "SELECT c.CustomerName, dt.Amount FROM Customers c " +
            "INNER JOIN (SELECT CustomerID, Amount FROM Orders WHERE Orders.CustomerID = c.ID) AS dt " +
            "ON c.ID = dt.CustomerID");
        var selectDefinition = grammar.CreateSelect(node, db, db);

        // Resolution must succeed — the outer alias `c` is now visible inside the derived table.
        Assert.False(selectDefinition.InvalidReferences,
            $"Reference resolution failed: {selectDefinition.InvalidReferenceReason}");
    }

    [Fact]
    public void Scenario_NestedDerivedTables_InnerReferencesOutermostColumn()
    {
        // Issue #182: nested derived tables (3-deep) — the innermost SELECT references a
        // column from the outermost FROM. Each layer must propagate its own outer scope
        // (parent's outer + parent's siblings) into the next layer's resolver.
        var db = BuildSampleDatabase();
        var grammar = new AnsiSqlGrammar();

        var node = ParseHelper.Parse(grammar,
            "SELECT outer_t.CustomerName FROM Customers AS outer_t " +
            "INNER JOIN (" +
            "  SELECT mid.CustomerID FROM (" +
            "    SELECT inner_t.CustomerID FROM Orders AS inner_t WHERE inner_t.CustomerID = outer_t.ID" +
            "  ) AS mid" +
            ") AS dt ON outer_t.ID = dt.CustomerID");
        var selectDefinition = grammar.CreateSelect(node, db, db);

        Assert.False(selectDefinition.InvalidReferences,
            $"Reference resolution failed: {selectDefinition.InvalidReferenceReason}");
    }

    [Fact]
    public void Scenario_NonCorrelatedDerivedTable_ExecutesEndToEnd()
    {
        // Issue #182 / #190: a non-correlated derived table must resolve cleanly AND
        // execute end-to-end. The resolution-only limitation comment has been removed now
        // that QueryEngine.GetQueryableRowsInFromTable materializes SqlDerivedTable.
        // Orders: (100,1,50), (101,1,25), (102,2,75), (103,3,10), (104,3,30), (105,3,40)
        // WHERE Amount > 25 keeps: 50, 75, 30, 40 → 4 rows.
        var db = BuildSampleDatabase();

        var result = Run(
            "SELECT dt.CustomerID, dt.Amount FROM (SELECT CustomerID, Amount FROM Orders) AS dt " +
            "WHERE dt.Amount > 25",
            db);

        Assert.Equal(2, result.Columns.Count);
        Assert.Equal(4, result.Rows.Count);
        var amounts = result.Rows.Cast<DataRow>()
            .Select(r => Convert.ToDecimal(r["Amount"]))
            .OrderBy(a => a)
            .ToList();
        Assert.Equal(new[] { 30m, 40m, 50m, 75m }, amounts);
    }

    [Fact]
    public void Scenario_DerivedTable_AmbiguousOuterAndInnerColumn_ProducesClearError()
    {
        // Issue #182: when an unqualified column name appears in BOTH the inner derived
        // table's FROM and the threaded outer scope, the resolver flags it as ambiguous.
        // (Same behavior as today's EXISTS/scalar-subquery scope; surfaced via the
        // existing TableFinder concat-scope flow.)
        //
        // Customers and Orders both have an ID column. Both tables are aliased so the
        // unqualified `ID` cannot be matched by the alias-shortcut in GetMatchedTable —
        // the resolver falls through to HuntForPossibleTable, which sees ID in both
        // schemas (inner Orders + threaded outer Customers) and reports the column as
        // ambiguous.
        var db = BuildSampleDatabase();
        var grammar = new AnsiSqlGrammar();

        var node = ParseHelper.Parse(grammar,
            "SELECT c.CustomerName FROM Customers c " +
            "INNER JOIN (SELECT o.CustomerID FROM Orders o WHERE ID > 0) AS dt " +
            "ON c.ID = dt.CustomerID");
        var selectDefinition = grammar.CreateSelect(node, db, db);

        Assert.True(selectDefinition.InvalidReferences,
            $"Resolver should report the unqualified ID inside the derived table as ambiguous. Reason was: {selectDefinition.InvalidReferenceReason ?? "<none>"}");
        Assert.Contains("ambiguous", selectDefinition.InvalidReferenceReason!,
            StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Issue #190 — SqlDerivedTable in main FROM position: QueryEngine must
    // materialize the inner SELECT and expose its rows to the outer query.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Scenario_DerivedTable_SingleRowProjection_ReturnsExpectedRow()
    {
        // Issue #190: simplest derived-table case where the inner SELECT returns one row.
        // Order ID=100 belongs to CustomerID=1; the outer query projects just CustomerID.
        var db = BuildSampleDatabase();

        var result = Run(
            "SELECT sub.CustomerID FROM (SELECT CustomerID FROM Orders WHERE ID = 100) AS sub",
            db);

        Assert.Single(result.Columns);
        Assert.Single(result.Rows);
        Assert.Equal(1, Convert.ToInt32(result.Rows[0]["CustomerID"]));
    }

    [Fact]
    public void Scenario_DerivedTable_WithWhereClause_FiltersCorrectly()
    {
        // Issue #190: derived table with an outer WHERE clause applied to the
        // materialized result. The inner SELECT returns all 6 orders; the outer
        // WHERE Amount > 50 keeps 75 (Bob) → 1 row.
        var db = BuildSampleDatabase();

        var result = Run(
            "SELECT dt.CustomerID, dt.Amount FROM (SELECT CustomerID, Amount FROM Orders) AS dt " +
            "WHERE dt.Amount > 50",
            db);

        Assert.Equal(2, result.Columns.Count);
        Assert.Single(result.Rows);
        Assert.Equal(75m, Convert.ToDecimal(result.Rows[0]["Amount"]));
    }

    [Fact]
    public void Scenario_DerivedTable_WithInnerWhereClause_FiltersBeforeMaterializing()
    {
        // Issue #190: derived table whose inner SELECT contains its own WHERE.
        // Only orders from customer 1 are projected by the inner SELECT;
        // the outer query sees those 2 rows.
        var db = BuildSampleDatabase();

        var result = Run(
            "SELECT dt.CustomerID, dt.Amount FROM (SELECT CustomerID, Amount FROM Orders WHERE CustomerID = 1) AS dt",
            db);

        Assert.Equal(2, result.Columns.Count);
        Assert.Equal(2, result.Rows.Count);
        foreach (DataRow r in result.Rows)
            Assert.Equal(1, Convert.ToInt32(r["CustomerID"]));
    }

    [Fact]
    public void Scenario_UnaliasedTables_AmbiguousColumn_ProducesAmbiguityError()
    {
        // Issue #189: two unaliased tables in scope that both have a column with the
        // same name must produce an ambiguity error, not silently resolve to the first
        // table. Prior to the fix, GetMatchedTableInternal returned the first table
        // whenever both table.TableAlias and columnRef.TableName were null/empty
        // (string.Compare(null, null) == 0), bypassing HuntForPossibleTable entirely.
        //
        // Customers and Orders both have an ID column. With no aliases, an unqualified
        // reference to ID in a two-table JOIN should be flagged as ambiguous.
        var db = BuildSampleDatabase();
        var grammar = new AnsiSqlGrammar();

        var node = ParseHelper.Parse(grammar,
            "SELECT ID FROM Customers INNER JOIN Orders ON Customers.ID = Orders.CustomerID");
        var selectDefinition = grammar.CreateSelect(node, db, db);

        Assert.True(selectDefinition.InvalidReferences,
            $"Resolver should flag unqualified ID as ambiguous across two unaliased tables. Reason was: {selectDefinition.InvalidReferenceReason ?? "<none>"}");
        Assert.Contains("ambiguous", selectDefinition.InvalidReferenceReason!,
            StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Issue #205 — LEFT / RIGHT / FULL OUTER JOIN integration tests through
    // the full parse-to-execute stack. Each test exercises outer-join semantics:
    // null-padded rows appear for unmatched sides.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Scenario_LeftJoin_UnmatchedRowsReturnNull()
    {
        // Dan (CustomerID=4) has no orders; he appears as a null-padded row in LEFT JOIN.
        var db = BuildSampleDatabase();

        var result = Run(
            "SELECT c.CustomerName, o.Amount FROM Customers c LEFT JOIN Orders o ON c.ID = o.CustomerID",
            db);

        // 6 matched rows + 1 null row for Dan = 7 total
        Assert.Equal(7, result.Rows.Count);

        // Dan row has NULL Amount
        var danRows = result.Rows.Cast<DataRow>().Where(r => "Dan".Equals(r["CustomerName"])).ToList();
        Assert.Single(danRows);
        Assert.Equal(DBNull.Value, danRows[0]["Amount"]);

        // Alice has 2 rows, Carol has 3 rows
        Assert.Equal(2, result.Rows.Cast<DataRow>().Count(r => "Alice".Equals(r["CustomerName"])));
        Assert.Equal(3, result.Rows.Cast<DataRow>().Count(r => "Carol".Equals(r["CustomerName"])));
    }

    [Fact]
    public void Scenario_RightJoin_UnmatchedRowsReturnNull()
    {
        // Orphan order (CustomerID=99) has no matching Customer — appears with NULL CustomerName.
        var db = new InMemoryDatabase("Sales");
        var customers = db.AddTable("Customers", ("ID", typeof(int)), ("CustomerName", typeof(string)));
        customers.Rows.Add(1, "Alice");
        customers.Rows.Add(2, "Bob");
        var orders = db.AddTable("Orders", ("ID", typeof(int)), ("CustomerID", typeof(int)), ("Amount", typeof(decimal)));
        orders.Rows.Add(101, 1, 50.00m);   // matched to Alice
        orders.Rows.Add(102, 99, 25.00m);  // orphan — no customer

        var result = Run(
            "SELECT c.CustomerName, o.Amount FROM Customers c RIGHT JOIN Orders o ON c.ID = o.CustomerID",
            db);

        // 1 matched row (Alice+101) + 1 null row (orphan 102) = 2 rows
        Assert.Equal(2, result.Rows.Count);

        var orphanRows = result.Rows.Cast<DataRow>().Where(r => 25.00m.Equals(r["Amount"])).ToList();
        Assert.Single(orphanRows);
        Assert.Equal(DBNull.Value, orphanRows[0]["CustomerName"]);

        var aliceRows = result.Rows.Cast<DataRow>().Where(r => "Alice".Equals(r["CustomerName"])).ToList();
        Assert.Single(aliceRows);
        Assert.Equal(50.00m, (decimal)aliceRows[0]["Amount"]);
    }

    [Fact]
    public void Scenario_FullOuterJoin_BothSidesUnmatched()
    {
        // Charlie has no orders (left unmatched) and orphan order has no customer (right unmatched).
        var db = new InMemoryDatabase("Sales");
        var customers = db.AddTable("Customers", ("ID", typeof(int)), ("CustomerName", typeof(string)));
        customers.Rows.Add(1, "Alice");
        customers.Rows.Add(3, "Charlie");  // no orders
        var orders = db.AddTable("Orders", ("ID", typeof(int)), ("CustomerID", typeof(int)), ("Amount", typeof(decimal)));
        orders.Rows.Add(101, 1, 50.00m);   // matched to Alice
        orders.Rows.Add(102, 99, 25.00m);  // orphan — no customer

        var result = Run(
            "SELECT c.CustomerName, o.Amount FROM Customers c FULL JOIN Orders o ON c.ID = o.CustomerID",
            db);

        // 1 matched (Alice+101) + 1 left-only (Charlie, NULL amount) + 1 right-only (orphan, NULL name) = 3
        Assert.Equal(3, result.Rows.Count);

        var charlieRows = result.Rows.Cast<DataRow>().Where(r => "Charlie".Equals(r["CustomerName"])).ToList();
        Assert.Single(charlieRows);
        Assert.Equal(DBNull.Value, charlieRows[0]["Amount"]);

        var orphanRows = result.Rows.Cast<DataRow>().Where(r => 25.00m.Equals(r["Amount"])).ToList();
        Assert.Single(orphanRows);
        Assert.Equal(DBNull.Value, orphanRows[0]["CustomerName"]);
    }
}
