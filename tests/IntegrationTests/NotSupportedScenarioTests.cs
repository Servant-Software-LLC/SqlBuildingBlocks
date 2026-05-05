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
    public void Scenario_NtileWindowFunction_ThrowsNotSupported()
    {
        // Scenario 10: hits a Wave 2 throw site end-to-end.
        // QueryEngine knows NTILE as WindowFunctionType.Ntile but its window-function
        // execution switch only implements ROW_NUMBER, RANK, DENSE_RANK, LAG, LEAD —
        // NTILE falls through to a NotSupportedException with a clear message.
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

        var ex = Assert.Throws<NotSupportedException>(() => engine.QueryAsDataTable());
        Assert.Contains("NTILE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "FINDING: QueryEngine.ExecuteCte does not implement recursive expansion. " +
                  "Recursive CTEs (WITH RECURSIVE) parse correctly but cannot be executed end-to-end. " +
                  "When the engine grows recursive support, this integration test should be activated.")]
    public void Scenario_RecursiveCte_HierarchyTraversal_NotImplemented()
    {
        // Placeholder for the recursive-CTE integration scenario (FINDING from Wave 2).
        // Kept as a Skip so the gap is visible in test output and so a future engineer
        // who fixes ExecuteCte has a one-flip enable path.
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
