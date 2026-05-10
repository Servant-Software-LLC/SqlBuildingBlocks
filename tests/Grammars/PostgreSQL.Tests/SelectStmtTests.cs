using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.PostgreSQL.Tests;

/// <summary>
/// PostgreSQL grammar tests at the SelectStmt root. Today the PostgreSQL grammar reuses
/// the core <see cref="SqlBuildingBlocks.SelectStmt"/> directly (no PostgreSQL-specific
/// SelectStmt class yet), so this also serves as a smoke-test that core SELECT features
/// reach a consumer wiring PostgreSQL.
/// </summary>
public class SelectStmtTests
{
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
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            PostgreSQL.DataType dataType = new(this);
            expr.InitializeRule(selectStmt, funcCall, dataType);
            expr.AddPgCastSupport(this, dataType);

            Root = selectStmt;
        }

        public SqlSelectDefinition Create(ParseTreeNode selectStmt) =>
            ((SelectStmt)Root).Create(selectStmt);
    }

    [Fact]
    public void Select_WindowFrame_RangeIntervalDayPreceding_Parses()
    {
        // Issue #180: PostgreSQL grammar must accept SQL:2003 INTERVAL-literal bounds
        // in RANGE-mode window frames (the standard form; PostgreSQL's RANGE INTERVAL
        // window-frame syntax is also SQL:2003-compliant).
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT EventDate, " +
            "SUM(Amount) OVER (ORDER BY EventDate RANGE BETWEEN INTERVAL '30' MINUTE PRECEDING AND CURRENT ROW) AS rolling " +
            "FROM Events");

        var selectStmt = grammar.Create(node);
        var aggregate = Assert.IsType<SqlAggregate>(selectStmt.Columns[1]);
        var frame = aggregate.WindowSpecification!.Frame!;
        Assert.Equal(WindowFrameMode.Range, frame.Mode);
        Assert.Equal(SqlWindowFrameBoundOffsetKind.Interval, frame.Start.Offset!.Kind);
        Assert.Equal(30L, frame.Start.Offset.Interval!.Magnitude);
        Assert.Equal(IntervalQualifier.Minute, frame.Start.Offset.Interval.Qualifier);
        Assert.Equal(WindowFrameBoundType.CurrentRow, frame.End!.Type);
    }
}
