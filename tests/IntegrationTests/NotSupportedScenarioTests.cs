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

    [Fact(Skip = "FINDING: QueryEngine has no support for RANGE-mode frames with INTERVAL bounds. " +
                  "FROM Wave 2: WindowFrameMode.Range exists but GetFrameBoundIndex treats the offset " +
                  "as a row count regardless of mode. End-to-end consumer scenario stays skipped " +
                  "until INTERVAL-bounded RANGE frames are implemented.")]
    public void Scenario_RangeIntervalWindowFrame_NotImplemented()
    {
        // Placeholder for the INTERVAL-bounded RANGE frame scenario (FINDING from Wave 2).
    }
}
