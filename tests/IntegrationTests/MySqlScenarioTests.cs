using SqlBuildingBlocks.IntegrationTests.Infrastructure;
using SqlBuildingBlocks.QueryProcessing;
using SqlBuildingBlocks.Utils;
using System.Data;
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
}
