using BenchmarkDotNet.Attributes;
using Irony.Parsing;
using SqlBuildingBlocks.Benchmarks.Infrastructure;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.QueryProcessing;
using SqlBuildingBlocks.Utils;
using System.Data;

namespace SqlBuildingBlocks.Benchmarks.Benchmarks;

/// <summary>
/// Measures the QueryEngine execution path: parsed SqlSelectDefinition -> in-memory
/// row set. The parse + Create work happens in <see cref="GlobalSetup"/> so each
/// iteration is dominated by the engine's expression-tree compile and LINQ execution.
///
/// This number is the foundation for #129. If reflection-based generic dispatch is
/// to be replaced with cached delegates or expression trees, the gain must be visible
/// here (especially in <see cref="ExecuteJoinedSelect"/> which exercises join LINQ).
/// </summary>
[MemoryDiagnoser]
public class QueryEngineBenchmarks
{
    private const int RowsPerTable = 100;

    private AnsiSqlGrammar grammar = null!;
    private LanguageData language = null!;
    private InMemoryDatabase database = null!;
    private IAllTableDataProvider provider = null!;

    private SqlSelectDefinition simpleSelect = null!;
    private SqlSelectDefinition joinedSelect = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        grammar = new AnsiSqlGrammar();
        language = new LanguageData(grammar);
        database = BuildDatabase(RowsPerTable);
        provider = new AllTableDataProvider(new[] { (ITableDataProvider)database });

        // Pre-parse the SQL so the iteration measures only the engine.
        simpleSelect = ParseAndCreate("SELECT ID, CustomerName FROM Customers");
        joinedSelect = ParseAndCreate(
            "SELECT c.CustomerName, o.Amount FROM Customers c INNER JOIN Orders o ON c.ID = o.CustomerID");
    }

    [Benchmark]
    public DataTable ExecuteSimpleSelect()
    {
        var engine = new QueryEngine(provider, simpleSelect);
        return engine.QueryAsDataTable();
    }

    [Benchmark]
    public DataTable ExecuteJoinedSelect()
    {
        var engine = new QueryEngine(provider, joinedSelect);
        return engine.QueryAsDataTable();
    }

    private SqlSelectDefinition ParseAndCreate(string sql)
    {
        var parser = new Parser(language);
        var tree = parser.Parse(sql);
        if (tree.HasErrors())
            throw new InvalidOperationException("Benchmark setup parse failed: " + sql);
        return grammar.CreateSelect(tree.Root, database, database);
    }

    private static InMemoryDatabase BuildDatabase(int rowsPerTable)
    {
        var db = new InMemoryDatabase("Sales");

        var customers = db.AddTable("Customers",
            ("ID", typeof(int)),
            ("CustomerName", typeof(string)),
            ("Region", typeof(string)));
        for (int i = 1; i <= rowsPerTable; i++)
            customers.Rows.Add(i, $"Customer_{i}", i % 4 == 0 ? "North" : "South");

        var orders = db.AddTable("Orders",
            ("ID", typeof(int)),
            ("CustomerID", typeof(int)),
            ("Amount", typeof(decimal)));
        for (int i = 1; i <= rowsPerTable; i++)
            orders.Rows.Add(i, ((i - 1) % rowsPerTable) + 1, (decimal)(i * 1.5));

        return db;
    }
}
