using SqlBuildingBlocks.IntegrationTests.Infrastructure;
using SqlBuildingBlocks.QueryProcessing;
using SqlBuildingBlocks.Utils;
using System.Data;
using System.Linq;
using Xunit;

namespace SqlBuildingBlocks.IntegrationTests;

public class PostgreSqlScenarioTests
{
    private static InMemoryDatabase BuildSampleDatabase()
    {
        var db = new InMemoryDatabase("Sales");

        var users = db.AddTable("users",
            ("id", typeof(int)),
            ("name", typeof(string)),
            ("region", typeof(string)));
        users.Rows.Add(1, "Alice", "North");
        users.Rows.Add(2, "Bob", "South");
        users.Rows.Add(3, "Carol", "North");

        return db;
    }

    [Fact]
    public void Scenario_PostgreSqlSelect_ParsesAndExecutes()
    {
        // Sanity: PostgreSQL grammar drives the full stack for a basic SELECT too.
        var db = BuildSampleDatabase();
        var grammar = new PostgreSqlSelectGrammar();
        var node = ParseHelper.Parse(grammar, "SELECT id, name FROM users WHERE region = 'North'");
        var selectDefinition = grammar.CreateSelect(node, db, db);
        Assert.False(selectDefinition.InvalidReferences);

        var allTableProvider = new AllTableDataProvider(new[] { (SqlBuildingBlocks.Interfaces.ITableDataProvider)db });
        var engine = new QueryEngine(allTableProvider, selectDefinition);
        var result = engine.QueryAsDataTable();

        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public void Scenario_NonRecursiveCte_ExecutesEndToEnd()
    {
        // Cross-dialect parity: non-recursive CTE works through the PostgreSQL grammar.
        var db = BuildSampleDatabase();
        var grammar = new PostgreSqlSelectGrammar();
        var node = ParseHelper.Parse(grammar,
            "WITH NorthUsers AS (SELECT id, name FROM users WHERE region = 'North') SELECT id FROM NorthUsers");
        var selectDefinition = grammar.CreateSelect(node, db, db);
        Assert.False(selectDefinition.InvalidReferences,
            $"Reference resolution failed: {selectDefinition.InvalidReferenceReason}");

        var allTableProvider = new AllTableDataProvider(new[] { (SqlBuildingBlocks.Interfaces.ITableDataProvider)db });
        var engine = new QueryEngine(allTableProvider, selectDefinition);
        var result = engine.QueryAsDataTable();

        // North region has Alice and Carol → 2 rows.
        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public void Scenario_DecimalLiteralComparison_FiltersByPriceGreaterThan()
    {
        // Issue #184: dialect parity — PostgreSQL grammar must also handle unsuffixed
        // fractional literals against decimal-typed columns end-to-end.
        var db = new InMemoryDatabase("Sales");
        var products = db.AddTable("products",
            ("id", typeof(int)),
            ("name", typeof(string)),
            ("price", typeof(decimal)));
        products.Rows.Add(1, "Apple", 1.50m);
        products.Rows.Add(2, "Bread", 3.00m);
        products.Rows.Add(3, "Cheese", 6.25m);
        products.Rows.Add(4, "Donut", 0.75m);

        var grammar = new PostgreSqlSelectGrammar();
        var node = ParseHelper.Parse(grammar, "SELECT id, price FROM products WHERE price > 3.00");
        var selectDefinition = grammar.CreateSelect(node, db, db);
        Assert.False(selectDefinition.InvalidReferences);

        var allTableProvider = new AllTableDataProvider(new[] { (SqlBuildingBlocks.Interfaces.ITableDataProvider)db });
        var engine = new QueryEngine(allTableProvider, selectDefinition);
        var result = engine.QueryAsDataTable();

        // Prices > 3.00 are 6.25 (Cheese) only — 1.50, 3.00, and 0.75 are excluded.
        Assert.Single(result.Rows);
        Assert.Equal(6.25m, (decimal)result.Rows[0]["price"]);
    }

    [Fact]
    public void Scenario_DerivedTable_CrossDialectSmoke_PostgreSQL()
    {
        // Issue #190 cross-dialect smoke: derived table in FROM position works through the
        // PostgreSQL grammar.
        var db = BuildSampleDatabase();
        var grammar = new PostgreSqlSelectGrammar();
        var node = ParseHelper.Parse(grammar,
            "SELECT dt.id, dt.name FROM (SELECT id, name FROM users WHERE region = 'North') AS dt");
        var selectDefinition = grammar.CreateSelect(node, db, db);
        Assert.False(selectDefinition.InvalidReferences,
            $"Reference resolution failed: {selectDefinition.InvalidReferenceReason}");

        var allTableProvider = new AllTableDataProvider(new[] { (SqlBuildingBlocks.Interfaces.ITableDataProvider)db });
        var engine = new QueryEngine(allTableProvider, selectDefinition);
        var result = engine.QueryAsDataTable();

        // North users are Alice and Carol → 2 rows.
        Assert.Equal(2, result.Rows.Count);
        var names = result.Rows.Cast<DataRow>().Select(r => (string)r["name"]).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "Alice", "Carol" }, names);
    }

    [Fact]
    public void Scenario_InsertReturning_ParsesEndToEnd()
    {
        // Scenario 7: PostgreSQL RETURNING — INSERT ... RETURNING.
        // The QueryEngine has no INSERT executor (only SELECT is executable today),
        // so this scenario validates the parse + AST construction round-trip end-to-end.
        // That is the integration boundary a consumer hits when wiring INSERT...RETURNING
        // through SqlBuildingBlocks: parse to a SqlInsertDefinition with ReturningClause populated.
        var grammar = new PostgreSqlInsertGrammar();
        var node = ParseHelper.Parse(grammar,
            "INSERT INTO users (id, name, region) VALUES (4, 'Dan', 'East') RETURNING id, name");

        var insertDef = grammar.CreateInsert(node);

        Assert.Equal("users", insertDef.Table!.TableName);
        Assert.Equal(3, insertDef.Columns.Count);
        Assert.NotNull(insertDef.Values);
        Assert.Single(insertDef.Values!);

        Assert.NotNull(insertDef.ReturningClause);
        Assert.Equal(2, insertDef.ReturningClause!.Items.Count);
        Assert.Equal("id", insertDef.ReturningClause.Items[0].Expression!.Column!.ColumnName);
        Assert.Equal("name", insertDef.ReturningClause.Items[1].Expression!.Column!.ColumnName);
    }
}
