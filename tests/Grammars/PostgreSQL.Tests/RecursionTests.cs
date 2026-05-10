using System.Text;
using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using Xunit;

namespace SqlBuildingBlocks.Grammars.PostgreSQL.Tests;

/// <summary>
/// Stack-overflow safety tests for deeply nested expressions and subqueries in the
/// PostgreSQL grammar (issue #203). Mirrors <c>AnsiSQL.Tests.RecursionTests</c>.
///
/// These tests assert that the parser never raises an unhandled <see cref="StackOverflowException"/>
/// when fed pathological input. Either a successful parse or a graceful
/// <see cref="ParseTree.HasErrors"/> is acceptable -- only a process abort (SO crash) is a failure.
///
/// Note: <see cref="StackOverflowException"/> cannot be caught with try/catch in .NET; it crashes
/// the process. The test runner reports this as a process abort, which is the failure signal.
/// </summary>
public class RecursionTests
{
    private const int DeepNestingDepth = 200;

    private class TestGrammar : Grammar
    {
        public TestGrammar() : base(false) // SQL is case insensitive
        {
            SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            PostgreSQL.Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            PostgreSQL.DataType dataType = new(this);
            expr.InitializeRule(selectStmt, funcCall, dataType);
            expr.AddPgCastSupport(this, dataType);

            Root = selectStmt;
        }
    }

    [Fact]
    public void DeeplyNestedParentheses_DoesNotCrash()
    {
        TestGrammar grammar = new();
        string sql = BuildNestedParenthesesSelect(DeepNestingDepth);

        // We deliberately call ParseTree (not Parse) so that ParseTree.HasErrors() is allowed.
        // The acceptance criterion is: do not crash the process. Either a successful parse or
        // a graceful parser error is acceptable.
        var parseTree = GrammarParser.ParseTree(grammar, sql);

        // The only assertion: we got back a ParseTree (no StackOverflowException).
        // HasErrors may be true or false; both are acceptable.
        Assert.NotNull(parseTree);
    }

    [Fact]
    public void DeeplyNestedBinaryExpression_DoesNotCrash()
    {
        TestGrammar grammar = new();
        string sql = BuildNestedBinaryExpressionSelect(DeepNestingDepth);

        var parseTree = GrammarParser.ParseTree(grammar, sql);

        Assert.NotNull(parseTree);
    }

    [Fact]
    public void DeeplyNestedSubqueries_DoesNotCrash()
    {
        TestGrammar grammar = new();
        string sql = BuildNestedSubquerySelect(DeepNestingDepth);

        var parseTree = GrammarParser.ParseTree(grammar, sql);

        Assert.NotNull(parseTree);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void NestingDepthRamp_ParenthesesAndSubqueries_DoesNotCrash(int depth)
    {
        // Ramp test that documents the practical depth limit. Each invocation must complete
        // without aborting the process. Whether HasErrors becomes true at higher depths is
        // observed but not asserted -- this test simply documents behavior across depths.
        TestGrammar grammar = new();

        var parenSql = BuildNestedParenthesesSelect(depth);
        var subquerySql = BuildNestedSubquerySelect(depth);

        var parenTree = GrammarParser.ParseTree(grammar, parenSql);
        var subqueryTree = GrammarParser.ParseTree(grammar, subquerySql);

        Assert.NotNull(parenTree);
        Assert.NotNull(subqueryTree);
    }

    /// <summary>
    /// Builds: SELECT ((((1)))) FROM t  -- with <paramref name="depth"/> levels of nested parens.
    /// </summary>
    private static string BuildNestedParenthesesSelect(int depth)
    {
        var sb = new StringBuilder("SELECT ");
        for (int i = 0; i < depth; i++) sb.Append('(');
        sb.Append('1');
        for (int i = 0; i < depth; i++) sb.Append(')');
        sb.Append(" FROM t");
        return sb.ToString();
    }

    /// <summary>
    /// Builds: SELECT ((((a + b) + c) + d) + ...) FROM t  -- with <paramref name="depth"/> nested
    /// binary additions, each wrapped in parens to force the parse tree to be deeply right-leaning
    /// rather than relying on operator associativity flattening.
    /// </summary>
    private static string BuildNestedBinaryExpressionSelect(int depth)
    {
        // Build inner-out: start with "a + b", then each level wraps "(prev + c)".
        var sb = new StringBuilder("SELECT ");
        for (int i = 0; i < depth; i++) sb.Append('(');
        sb.Append("a + b");
        for (int i = 0; i < depth; i++)
        {
            sb.Append(" + c)");
        }
        sb.Append(" FROM t");
        return sb.ToString();
    }

    /// <summary>
    /// Builds: SELECT * FROM (SELECT * FROM (SELECT * FROM (... SELECT * FROM t ...) x1) x2) xN
    /// with <paramref name="depth"/> levels of nested derived tables (each requires an alias).
    /// </summary>
    private static string BuildNestedSubquerySelect(int depth)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < depth; i++)
        {
            sb.Append("SELECT * FROM (");
        }
        sb.Append("SELECT * FROM t");
        for (int i = 0; i < depth; i++)
        {
            sb.Append(") x").Append(i);
        }
        return sb.ToString();
    }
}
