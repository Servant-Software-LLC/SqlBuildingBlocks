using SqlBuildingBlocks.IntegrationTests.Infrastructure;
using SqlBuildingBlocks.QueryProcessing;
using SqlBuildingBlocks.Utils;
using Xunit;

namespace SqlBuildingBlocks.IntegrationTests;

/// <summary>
/// End-to-end scenarios that document features the engine does NOT yet execute.
/// A consumer who issues these SQL strings should observe a clean failure surface
/// (NotSupportedException with a descriptive message) rather than wrong results.
/// </summary>
public class NotSupportedScenarioTests
{
    [Fact]
    public void Scenario_NtileWindowFunction_BucketsRowsCorrectly()
    {
        // Issue #169: NTILE(N) is now executed by the QueryEngine. End-to-end scenario:
        // four rows ordered by Salary, NTILE(2) buckets them into {1,1,2,2}.
        var db = new InMemoryDatabase("Sales");
        var employees = db.AddTable("Employees",
            ("Name", typeof(string)),
            ("Salary", typeof(decimal)));
        employees.Rows.Add("Alice", 50000m);
        employees.Rows.Add("Bob", 60000m);
        employees.Rows.Add("Carol", 70000m);
        employees.Rows.Add("Dan", 80000m);

        var grammar = new AnsiSqlGrammar();
        var node = ParseHelper.Parse(grammar,
            "SELECT Name, NTILE(2) OVER (ORDER BY Salary) AS bucket FROM Employees");
        var selectDefinition = grammar.CreateSelect(node, db, db);
        Assert.False(selectDefinition.InvalidReferences);

        var allTableProvider = new AllTableDataProvider(new[] { (SqlBuildingBlocks.Interfaces.ITableDataProvider)db });
        var engine = new QueryEngine(allTableProvider, selectDefinition);
        var result = engine.QueryAsDataTable();

        Assert.Equal(4, result.Rows.Count);
        var byName = result.Rows.Cast<System.Data.DataRow>().ToDictionary(r => (string)r["Name"]);
        Assert.Equal(1, byName["Alice"]["bucket"]);
        Assert.Equal(1, byName["Bob"]["bucket"]);
        Assert.Equal(2, byName["Carol"]["bucket"]);
        Assert.Equal(2, byName["Dan"]["bucket"]);
    }

    [Fact]
    public void Scenario_RecursiveCte_HierarchyTraversal_TraversesAllLevels()
    {
        // Issue #168: end-to-end execution of a parsed (rather than hand-built) recursive CTE.
        // WITH RECURSIVE org AS (
        //   SELECT id, parent_id FROM Hierarchy WHERE parent_id = 0
        //   UNION ALL
        //   SELECT h.id, h.parent_id FROM Hierarchy h JOIN org ON h.parent_id = org.id
        // ) SELECT id FROM org
        //
        // We use parent_id=0 as a "no-parent" sentinel rather than NULL — the engine's
        // expression builder cannot promote DBNull to int in JOIN equality predicates.
        var db = new InMemoryDatabase("Hr");
        var hierarchy = db.AddTable("Hierarchy",
            ("id", typeof(int)),
            ("parent_id", typeof(int)));
        // Tree: 1=root (parent=0); 2,3 → 1; 4,5 → 2 → 5 employees total.
        hierarchy.Rows.Add(1, 0);
        hierarchy.Rows.Add(2, 1);
        hierarchy.Rows.Add(3, 1);
        hierarchy.Rows.Add(4, 2);
        hierarchy.Rows.Add(5, 2);

        var grammar = new AnsiSqlGrammar();
        var node = ParseHelper.Parse(grammar,
            "WITH RECURSIVE org AS (" +
            "SELECT id, parent_id FROM Hierarchy WHERE parent_id = 0 " +
            "UNION ALL " +
            "SELECT h.id, h.parent_id FROM Hierarchy h JOIN org ON h.parent_id = org.id) " +
            "SELECT id FROM org");
        var selectDefinition = grammar.CreateSelect(node, db, db);
        Assert.False(selectDefinition.InvalidReferences,
            $"Reference resolution failed: {selectDefinition.InvalidReferenceReason}");

        var allTableProvider = new AllTableDataProvider(new[] { (SqlBuildingBlocks.Interfaces.ITableDataProvider)db });
        var engine = new QueryEngine(allTableProvider, selectDefinition);
        var result = engine.QueryAsDataTable();

        Assert.Equal(5, result.Rows.Count);
        var ids = result.Rows.Cast<System.Data.DataRow>()
            .Select(r => Convert.ToInt32(r["id"]))
            .OrderBy(i => i).ToArray();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, ids);
    }

    [Fact]
    public void Scenario_RangeIntervalWindowFrame_ProducesRollingSumOverDateRange()
    {
        // Issue #180 (Wave 14b lane A): the previously-skipped FINDING placeholder now executes
        // end-to-end. Wave 8 closed the engine + logical-entity layer for INTERVAL frame bounds;
        // Wave 14b adds the grammar layer so a consumer SQL string of the form
        //   RANGE BETWEEN INTERVAL '1' DAY PRECEDING AND CURRENT ROW
        // parses, resolves, and executes. This is the consumer-facing oracle.
        var db = new InMemoryDatabase("Sales");
        var events = db.AddTable("Events",
            ("EventDate", typeof(DateTime)),
            ("Amount", typeof(int)));
        events.Rows.Add(new DateTime(2026, 1, 1), 10);
        events.Rows.Add(new DateTime(2026, 1, 2), 20);
        events.Rows.Add(new DateTime(2026, 1, 3), 30);
        // Boundary check: skip Jan 4, so the Jan 5 row's window is just itself.
        events.Rows.Add(new DateTime(2026, 1, 5), 40);
        events.Rows.Add(new DateTime(2026, 1, 6), 50);

        var grammar = new AnsiSqlGrammar();
        var node = ParseHelper.Parse(grammar,
            "SELECT EventDate, " +
            "SUM(Amount) OVER (ORDER BY EventDate RANGE BETWEEN INTERVAL '1' DAY PRECEDING AND CURRENT ROW) AS rolling " +
            "FROM Events");
        var selectDefinition = grammar.CreateSelect(node, db, db);
        Assert.False(selectDefinition.InvalidReferences,
            $"Reference resolution failed: {selectDefinition.InvalidReferenceReason}");

        var allTableProvider = new AllTableDataProvider(new[] { (SqlBuildingBlocks.Interfaces.ITableDataProvider)db });
        var engine = new QueryEngine(allTableProvider, selectDefinition);
        var result = engine.QueryAsDataTable();

        Assert.Equal(5, result.Rows.Count);
        // The rolling-sum window is "rows within 1 day previous, plus current".
        Assert.Equal(10, Convert.ToInt32(result.Rows[0]["rolling"]));  // Jan 1: itself.
        Assert.Equal(30, Convert.ToInt32(result.Rows[1]["rolling"]));  // Jan 2: 10 + 20.
        Assert.Equal(50, Convert.ToInt32(result.Rows[2]["rolling"]));  // Jan 3: 20 + 30.
        Assert.Equal(40, Convert.ToInt32(result.Rows[3]["rolling"]));  // Jan 5: itself (Jan 4 missing).
        Assert.Equal(90, Convert.ToInt32(result.Rows[4]["rolling"]));  // Jan 6: 40 + 50.
    }
}
