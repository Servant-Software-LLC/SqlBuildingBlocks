using SqlBuildingBlocks.IntegrationTests.Infrastructure;
using SqlBuildingBlocks.QueryProcessing;
using SqlBuildingBlocks.Utils;
using System.Data;
using System.Linq;
using Xunit;

namespace SqlBuildingBlocks.IntegrationTests;

public class MySqlScenarioTests
{
    private static InMemoryDatabase BuildSampleDatabase()
    {
        var db = new InMemoryDatabase("Sales");

        var orders = db.AddTable("Orders",
            ("ID", typeof(int)),
            ("CustomerID", typeof(int)),
            ("Amount", typeof(decimal)));
        orders.Rows.Add(1, 10, 100.00m);
        orders.Rows.Add(2, 11, 200.00m);
        orders.Rows.Add(3, 12, 300.00m);
        orders.Rows.Add(4, 13, 400.00m);
        orders.Rows.Add(5, 14, 500.00m);
        orders.Rows.Add(6, 15, 600.00m);

        return db;
    }

    private static DataTable Run(string sql, InMemoryDatabase db)
    {
        var grammar = new MySqlGrammar();
        var node = ParseHelper.Parse(grammar, sql);
        var selectDefinition = grammar.CreateSelect(node, db, db);
        Assert.False(selectDefinition.InvalidReferences,
            $"Reference resolution failed for SQL: {sql}");

        var allTableProvider = new AllTableDataProvider(new[] { (SqlBuildingBlocks.Interfaces.ITableDataProvider)db });
        var engine = new QueryEngine(allTableProvider, selectDefinition);
        return engine.QueryAsDataTable();
    }

    [Fact]
    public void Scenario_LimitOffset_ReturnsExpectedSlice()
    {
        // Scenario 6: MySQL LIMIT/OFFSET — dialect-specific syntax.
        var db = BuildSampleDatabase();

        var result = Run("SELECT ID, Amount FROM Orders ORDER BY ID LIMIT 2 OFFSET 2", db);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(3, Convert.ToInt32(result.Rows[0]["ID"]));
        Assert.Equal(4, Convert.ToInt32(result.Rows[1]["ID"]));
    }

    [Fact]
    public void Scenario_NonRecursiveCte_ExecutesEndToEnd()
    {
        // Cross-dialect parity: non-recursive CTE works through the MySQL grammar too.
        var db = BuildSampleDatabase();

        var result = Run(
            "WITH BigOrders AS (SELECT ID, Amount FROM Orders WHERE Amount > 250) SELECT ID FROM BigOrders ORDER BY ID",
            db);

        // Amounts > 250 are 300, 400, 500, 600 → IDs 3, 4, 5, 6.
        Assert.Equal(4, result.Rows.Count);
        var ids = result.Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r["ID"])).ToList();
        Assert.Equal(new[] { 3, 4, 5, 6 }, ids);
    }

    [Fact]
    public void Scenario_DecimalLiteralComparison_FiltersByAmountGreaterThan()
    {
        // Issue #184: dialect parity — MySQL grammar must also handle unsuffixed fractional
        // literals against decimal-typed columns end-to-end.
        var db = BuildSampleDatabase();

        var result = Run("SELECT ID, Amount FROM Orders WHERE Amount > 250.00", db);

        // Amounts > 250 are 300, 400, 500, 600 → 4 rows.
        Assert.Equal(4, result.Rows.Count);
        Assert.All(result.Rows.Cast<DataRow>(), r => Assert.True((decimal)r["Amount"] > 250.00m));
    }

    [Fact]
    public void Scenario_RecursiveCte_HierarchyTraversal_ExecutesEndToEnd()
    {
        // Cross-dialect parity for issue #168 — WITH RECURSIVE through the MySQL grammar.
        // MySQL natively supports `WITH RECURSIVE` so this exercises the full parse → resolve
        // → execute pipeline against the dialect grammar.
        // Uses parent_id=0 as the "no-parent" sentinel — the engine's expression-builder
        // cannot promote DBNull to int in JOIN equality predicates.
        var db = new InMemoryDatabase("Hr");
        var hierarchy = db.AddTable("Hierarchy",
            ("id", typeof(int)),
            ("parent_id", typeof(int)));
        hierarchy.Rows.Add(1, 0);
        hierarchy.Rows.Add(2, 1);
        hierarchy.Rows.Add(3, 1);
        hierarchy.Rows.Add(4, 2);

        var result = Run(
            "WITH RECURSIVE descendants AS (" +
            "SELECT id, parent_id FROM Hierarchy WHERE parent_id = 0 " +
            "UNION ALL " +
            "SELECT h.id, h.parent_id FROM Hierarchy h JOIN descendants d ON h.parent_id = d.id) " +
            "SELECT id FROM descendants",
            db);

        Assert.Equal(4, result.Rows.Count);
        var ids = result.Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r["id"])).OrderBy(i => i).ToArray();
        Assert.Equal(new[] { 1, 2, 3, 4 }, ids);
    }

    [Fact]
    public void Scenario_RecursiveCte_JoinedWithOuterTable_ExecutesEndToEnd()
    {
        // Multi-level scenario: the recursive CTE result is joined against a non-CTE base
        // table in the outer SELECT. Exercises that the recursive CTE's projection schema
        // is correctly visible to the outer JOIN.
        var db = new InMemoryDatabase("Hr");
        var hierarchy = db.AddTable("Hierarchy",
            ("id", typeof(int)),
            ("parent_id", typeof(int)));
        hierarchy.Rows.Add(1, 0);
        hierarchy.Rows.Add(2, 1);
        hierarchy.Rows.Add(3, 1);

        var details = db.AddTable("Details",
            ("id", typeof(int)),
            ("name", typeof(string)));
        details.Rows.Add(1, "Root");
        details.Rows.Add(2, "Left");
        details.Rows.Add(3, "Right");

        var result = Run(
            "WITH RECURSIVE descendants AS (" +
            "SELECT id, parent_id FROM Hierarchy WHERE parent_id = 0 " +
            "UNION ALL " +
            "SELECT h.id, h.parent_id FROM Hierarchy h JOIN descendants d ON h.parent_id = d.id) " +
            "SELECT d.id, det.name FROM descendants d JOIN Details det ON d.id = det.id",
            db);

        Assert.Equal(3, result.Rows.Count);
        var byId = result.Rows.Cast<DataRow>().ToDictionary(r => Convert.ToInt32(r["id"]), r => (string)r["name"]);
        Assert.Equal("Root", byId[1]);
        Assert.Equal("Left", byId[2]);
        Assert.Equal("Right", byId[3]);
    }

    [Fact]
    public void Scenario_DerivedTable_CrossDialectSmoke_MySQL()
    {
        // Issue #190 cross-dialect smoke: derived table in FROM position works through the
        // MySQL grammar. Same SQL should round-trip through all dialects.
        var db = BuildSampleDatabase();

        var result = Run(
            "SELECT dt.CustomerID, dt.Amount FROM (SELECT CustomerID, Amount FROM Orders) AS dt " +
            "WHERE dt.Amount > 250",
            db);

        // Orders with Amount > 250: 300, 400, 500, 600 → 4 rows.
        Assert.Equal(4, result.Rows.Count);
        Assert.All(result.Rows.Cast<DataRow>(), r => Assert.True(Convert.ToDecimal(r["Amount"]) > 250m));
    }

    [Fact]
    public void Scenario_LeftJoin_CrossDialectSmoke_MySQL()
    {
        // Issue #205 smoke test: MySQL grammar parses LEFT JOIN correctly and produces null-padded rows.
        // Build a tiny DB: Customers (1=Alice, 2=Bob) with one order for Alice → Bob has no match.
        var db = new InMemoryDatabase("Sales");
        var customers = db.AddTable("Customers", ("ID", typeof(int)), ("Name", typeof(string)));
        customers.Rows.Add(1, "Alice");
        customers.Rows.Add(2, "Bob");  // no orders
        var orders = db.AddTable("Orders", ("ID", typeof(int)), ("CustomerID", typeof(int)), ("Amount", typeof(decimal)));
        orders.Rows.Add(101, 1, 100.00m);

        var result = Run(
            "SELECT c.Name, o.Amount FROM Customers c LEFT JOIN Orders o ON c.ID = o.CustomerID",
            db);

        // 1 matched (Alice) + 1 null row (Bob) = 2
        Assert.Equal(2, result.Rows.Count);
        var bobRows = result.Rows.Cast<DataRow>().Where(r => "Bob".Equals(r["Name"])).ToList();
        Assert.Single(bobRows);
        Assert.Equal(DBNull.Value, bobRows[0]["Amount"]);
    }
}
