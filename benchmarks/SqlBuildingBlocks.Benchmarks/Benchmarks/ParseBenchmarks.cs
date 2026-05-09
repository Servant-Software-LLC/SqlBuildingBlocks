using BenchmarkDotNet.Attributes;
using Irony.Parsing;
using SqlBuildingBlocks.Benchmarks.Infrastructure;
using System.Text;

namespace SqlBuildingBlocks.Benchmarks.Benchmarks;

/// <summary>
/// Measures the parse + AST construction hot path: Irony tokenization, parse-tree
/// build, and NonTerminal.Create() dispatch. The grammar (Irony LanguageData + Parser)
/// is constructed once in <see cref="GlobalSetup"/> so the per-iteration cost reflects
/// only the parse phase.
///
/// FINDINGS surfaced by this suite become the baseline for #129
/// (reflection-based generic dispatch in QueryEngine) and any future optimization PR.
/// </summary>
[MemoryDiagnoser]
public class ParseBenchmarks
{
    private const string SimpleSelect =
        "SELECT * FROM Customers";

    private const string ComplexSelect = @"
SELECT c.ID, c.CustomerName, COUNT(o.ID) AS OrderCount, SUM(o.Amount) AS TotalSpent
FROM Customers c
INNER JOIN Orders o ON c.ID = o.CustomerID
LEFT JOIN Regions r ON c.RegionID = r.ID
WHERE c.Status = 'Active' AND o.Amount > 100 AND r.Code IN ('N','S','E','W')
GROUP BY c.ID, c.CustomerName
ORDER BY TotalSpent DESC, c.CustomerName ASC";

    private const string CteSelect =
        "WITH AllOrders AS (SELECT ID, Amount FROM Orders) SELECT ID FROM AllOrders";

    private static readonly string DeeplyNestedExpression = BuildNestedExpression(50);

    private AnsiSqlGrammar ansiGrammar = null!;
    private MySqlGrammar mysqlGrammar = null!;
    private LanguageData ansiLanguage = null!;
    private LanguageData mysqlLanguage = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Build each grammar's LanguageData once. This is intentionally outside the
        // measured path — Irony grammar build is O(rules*lookahead) and is far slower
        // than a single parse.
        ansiGrammar = new AnsiSqlGrammar();
        mysqlGrammar = new MySqlGrammar();
        ansiLanguage = new LanguageData(ansiGrammar);
        mysqlLanguage = new LanguageData(mysqlGrammar);
    }

    [Benchmark]
    public ParseTree ParseSimpleSelect_Ansi() => Parse(ansiLanguage, SimpleSelect);

    [Benchmark]
    public ParseTree ParseSimpleSelect_MySql() => Parse(mysqlLanguage, SimpleSelect);

    [Benchmark]
    public ParseTree ParseComplexSelect_Ansi() => Parse(ansiLanguage, ComplexSelect);

    [Benchmark]
    public ParseTree ParseComplexSelect_MySql() => Parse(mysqlLanguage, ComplexSelect);

    [Benchmark]
    public ParseTree ParseDeeplyNestedExpression_Ansi() =>
        Parse(ansiLanguage, "SELECT * FROM T WHERE " + DeeplyNestedExpression);

    [Benchmark]
    public ParseTree ParseCte_Ansi() => Parse(ansiLanguage, CteSelect);

    /// <summary>
    /// Parse-and-Create benchmark for AnsiSQL: includes both the Irony parse and the
    /// recursive Create() walk that constructs the SqlSelectDefinition. This is the
    /// single most representative number for consumer code-paths that go SQL -> AST.
    /// </summary>
    [Benchmark]
    public object ParseAndCreate_ComplexSelect_Ansi()
    {
        var tree = Parse(ansiLanguage, ComplexSelect);
        return ansiGrammar.CreateSelect(tree.Root);
    }

    private static ParseTree Parse(LanguageData language, string sql)
    {
        // A fresh Parser each iteration matches consumer behavior (consumers do not
        // reuse the Parser across calls because it is not thread-safe). The parser
        // constructor is cheap; LanguageData construction is the expensive bit and
        // is amortized in GlobalSetup.
        var parser = new Parser(language);
        return parser.Parse(sql);
    }

    private static string BuildNestedExpression(int depth)
    {
        // Build "((((a = 1) AND (b = 2)) AND (c = 3)) ...)" with `depth` levels of
        // binary expression. Stays well under Wave 2's stack-overflow threshold.
        var sb = new StringBuilder();
        for (int i = 0; i < depth; i++)
            sb.Append('(');
        sb.Append("c0 = 0");
        for (int i = 1; i <= depth; i++)
            sb.Append(" AND c").Append(i).Append(" = ").Append(i).Append(')');
        return sb.ToString();
    }
}
