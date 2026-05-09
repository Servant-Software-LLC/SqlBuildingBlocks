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

    [Fact]
    public void Scenario_Top_LimitsRowCount()
    {
        // Scenario 8b: SQL Server TOP — execution-side integration.
        // QueryEngine now honors SqlSelectDefinition.Top by limiting the result to N rows
        // after ORDER BY. Source has 7 products; TOP 3 ORDER BY Price DESC must yield exactly
        // the top three priced rows (Cheese 6.25, Grape 5.50, Flour 4.00).
        var db = BuildSampleDatabase();
        var result = Run("SELECT TOP 3 ID, Name, Price FROM Products ORDER BY Price DESC", db);

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal("Cheese", result.Rows[0]["Name"]);
        Assert.Equal("Grape", result.Rows[1]["Name"]);
        Assert.Equal("Flour", result.Rows[2]["Name"]);
    }

    [Fact]
    public void Scenario_Top_Percent_ThrowsNotSupported()
    {
        // TOP PERCENT parses but the engine does not implement the percent semantics.
        // Verify the engine raises a clean NotSupportedException rather than returning wrong rows.
        var db = BuildSampleDatabase();
        var ex = Assert.Throws<NotSupportedException>(() =>
            Run("SELECT TOP 50 PERCENT ID FROM Products", db));
        Assert.Contains("PERCENT", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scenario_Top_WithTies_ThrowsNotSupported()
    {
        // TOP ... WITH TIES parses but the engine does not implement tie-extension semantics.
        // Verify the engine raises a clean NotSupportedException rather than silently dropping ties.
        var db = BuildSampleDatabase();
        var ex = Assert.Throws<NotSupportedException>(() =>
            Run("SELECT TOP 3 WITH TIES ID FROM Products ORDER BY Price DESC", db));
        Assert.Contains("WITH TIES", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
