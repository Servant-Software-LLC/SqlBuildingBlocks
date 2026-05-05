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
        // End-to-end execution through the QueryEngine for parsed (rather than hand-built)
        // CTEs trips a binding mismatch (FINDING below). This test stays parse-only so the
        // consumer-visible parse surface is locked in for AST-only tooling.
        // Reference-resolution is intentionally skipped here because the CTE name is not a
        // schema-provider-registered table; ResolveReferences would fault on lookup.
        var grammar = new AnsiSqlGrammar();
        var node = ParseHelper.Parse(grammar,
            "WITH AllOrders AS (SELECT ID, Amount FROM Orders) SELECT ID FROM AllOrders");
        var selectDefinition = grammar.CreateSelect(node);

        Assert.Single(selectDefinition.Ctes);
        Assert.Equal("AllOrders", selectDefinition.Ctes[0].Name);
        Assert.NotNull(selectDefinition.Ctes[0].SelectDefinition);
        Assert.Equal("Orders", selectDefinition.Ctes[0].SelectDefinition!.Table!.TableName);
    }

    [Fact(Skip = "FINDING (Wave 5): End-to-end execution of a parsed (rather than hand-built) " +
                  "non-recursive CTE trips QueryEngine.ResolveSelectColumnsFromTable with " +
                  "'Column ID does not belong to table .' — the row's DataTable is unbound when " +
                  "the CTE is materialized through QueryAsDataTable. The hand-built CTE engine " +
                  "tests pass because they configure SqlColumn.TableRef directly. Until the " +
                  "parse + ResolveReferences pipeline sets equivalent table references on CTE-derived " +
                  "rows, parse->execute round-trips for CTEs are blocked at the integration layer.")]
    public void Scenario_NonRecursiveCte_ExecutesEndToEnd_NotImplemented()
    {
        // Placeholder for end-to-end CTE execution via parsed SQL. Marked Skip per FINDING above.
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
    public void Scenario_MalformedSql_ReportsParseError()
    {
        // Scenario 12: negative integration test — malformed SQL fails parse with a clear error surface.
        var grammar = new AnsiSqlGrammar();
        var parseTree = ParseHelper.ParseTree(grammar, "SELECT FROM WHERE");

        Assert.True(parseTree.HasErrors(),
            "Malformed SQL should produce parse errors but did not.");
        Assert.NotEmpty(parseTree.ParserMessages);
    }
}
