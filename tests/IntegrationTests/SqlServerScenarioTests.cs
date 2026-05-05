using SqlBuildingBlocks.IntegrationTests.Infrastructure;
using SqlBuildingBlocks.QueryProcessing;
using SqlBuildingBlocks.Utils;
using System.Data;
using Xunit;

namespace SqlBuildingBlocks.IntegrationTests;

public class SqlServerScenarioTests
{
    private static InMemoryDatabase BuildSampleDatabase()
    {
        var db = new InMemoryDatabase("Sales");

        var products = db.AddTable("Products",
            ("ID", typeof(int)),
            ("Name", typeof(string)),
            ("Price", typeof(decimal)));
        products.Rows.Add(1, "Apple", 1.50m);
        products.Rows.Add(2, "Bread", 3.00m);
        products.Rows.Add(3, "Cheese", 6.25m);
        products.Rows.Add(4, "Donut", 0.75m);
        products.Rows.Add(5, "Egg", 2.10m);
        products.Rows.Add(6, "Flour", 4.00m);
        products.Rows.Add(7, "Grape", 5.50m);

        return db;
    }

    private static DataTable Run(string sql, InMemoryDatabase db)
    {
        var grammar = new SqlServerGrammar();
        var node = ParseHelper.Parse(grammar, sql);
        var selectDefinition = grammar.CreateSelect(node, db, db);
        Assert.False(selectDefinition.InvalidReferences,
            $"Reference resolution failed for SQL: {sql}");

        var allTableProvider = new AllTableDataProvider(new[] { (SqlBuildingBlocks.Interfaces.ITableDataProvider)db });
        var engine = new QueryEngine(allTableProvider, selectDefinition);
        return engine.QueryAsDataTable();
    }

    [Fact]
    public void Scenario_Top_ParsesToSqlTopClause()
    {
        // Scenario 8a: SQL Server TOP — parse-side integration.
        // SqlBuildingBlocks parses TOP into SqlSelectDefinition.Top, but the QueryEngine
        // does not currently apply Top during execution (FINDING below). This test verifies
        // the parse-side integration end-to-end so a consumer can rely on the AST.
        var db = BuildSampleDatabase();

        var grammar = new SqlServerGrammar();
        var node = ParseHelper.Parse(grammar, "SELECT TOP 5 ID, Price FROM Products ORDER BY Price DESC");
        var selectDefinition = grammar.CreateSelect(node, db, db);

        Assert.NotNull(selectDefinition.Top);
        Assert.Equal(5, selectDefinition.Top!.Count.Value);
        Assert.False(selectDefinition.Top.Percent);
        Assert.False(selectDefinition.Top.WithTies);
        Assert.False(selectDefinition.InvalidReferences);
    }

    [Fact(Skip = "FINDING (Wave 5): QueryEngine does not honor SqlTopClause during execution. " +
                  "SQL Server TOP parses into SqlSelectDefinition.Top correctly, but no code path " +
                  "in QueryEngine applies the row limit (Top is propagated through CTE copies but " +
                  "never read at result emission). LIMIT/OFFSET works because of explicit Skip/Take. " +
                  "Until QueryEngine wires Top into the result pipeline (mirroring Limit), end-to-end " +
                  "TOP execution silently returns the full set.")]
    public void Scenario_Top_LimitsRowCount_NotImplemented()
    {
        // Placeholder for the executable end-to-end TOP scenario, kept skipped per FINDING above.
    }
}
