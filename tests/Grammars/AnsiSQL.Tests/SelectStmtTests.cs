using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.AnsiSQL.Tests;

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
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            AnsiSQL.SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = selectStmt;
        }

        public virtual SqlSelectDefinition Create(ParseTreeNode selectStmt) =>
            ((SelectStmt)Root).Create(selectStmt);

        public virtual SqlSelectDefinition Create(ParseTreeNode selectStmt,
            IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider) =>
            ((SelectStmt)Root).Create(selectStmt, databaseConnectionProvider, tableSchemaProvider);
    }

    [Fact]
    public void Select_Star_FromSingleTable()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        Assert.IsType<SqlAllColumns>(selectStmt.Columns[0]);
        Assert.NotNull(selectStmt.Table);
        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.Empty(selectStmt.Joins);
        Assert.Null(selectStmt.WhereClause);
    }

    [Fact]
    public void Select_NamedColumns_FromTable()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT ID, CustomerName FROM Customers");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.Equal(2, selectStmt.Columns.Count);

        var firstColumn = Assert.IsType<SqlColumn>(selectStmt.Columns[0]);
        Assert.Equal("ID", firstColumn.ColumnName);

        var secondColumn = Assert.IsType<SqlColumn>(selectStmt.Columns[1]);
        Assert.Equal("CustomerName", secondColumn.ColumnName);

        Assert.Equal("Customers", selectStmt.Table.TableName);
    }

    [Fact]
    public void Select_WithWhereClause()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT ID FROM Customers WHERE CustomerName = 'Alice'");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.WhereClause);
        Assert.NotNull(selectStmt.WhereClause.BinExpr);
        Assert.Equal("CustomerName", selectStmt.WhereClause.BinExpr.Left.Column.ColumnName);
        Assert.Equal("Alice", selectStmt.WhereClause.BinExpr.Right!.Value!.String);
    }

    [Fact]
    public void Select_WithOrderBy_Asc()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT ID, CustomerName FROM Customers ORDER BY CustomerName");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.OrderBy);
        Assert.Single(selectStmt.OrderBy);
        Assert.Equal("CustomerName", selectStmt.OrderBy[0].ColumnName);
        Assert.False(selectStmt.OrderBy[0].Descending);
    }

    [Fact]
    public void Select_WithOrderBy_Desc()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT ID, CustomerName FROM Customers ORDER BY CustomerName DESC");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.OrderBy);
        Assert.Single(selectStmt.OrderBy);
        Assert.Equal("CustomerName", selectStmt.OrderBy[0].ColumnName);
        Assert.True(selectStmt.OrderBy[0].Descending);
    }

    [Fact]
    public void Select_WithOrderBy_MultipleColumns()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers ORDER BY State ASC, CustomerName DESC");

        var selectStmt = grammar.Create(node);

        Assert.Equal(2, selectStmt.OrderBy.Count);
        Assert.Equal("State", selectStmt.OrderBy[0].ColumnName);
        Assert.False(selectStmt.OrderBy[0].Descending);
        Assert.Equal("CustomerName", selectStmt.OrderBy[1].ColumnName);
        Assert.True(selectStmt.OrderBy[1].Descending);
    }

    [Fact]
    public void Select_WithGroupBy_SingleColumn()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT department, COUNT(*) FROM employees GROUP BY department");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        Assert.Single(selectStmt.GroupBy.Columns);
        Assert.Equal("department", selectStmt.GroupBy.Columns[0]);
        Assert.Empty(selectStmt.GroupBy.GroupingSets);
    }

    [Fact]
    public void Select_WithGroupBy_AndHaving()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT department, COUNT(*) FROM employees GROUP BY department HAVING department > 5");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        Assert.Single(selectStmt.GroupBy.Columns);
        Assert.Equal("department", selectStmt.GroupBy.Columns[0]);

        Assert.NotNull(selectStmt.HavingClause);
    }

    [Fact]
    public void Select_WithInnerJoin()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT c.CustomerName, o.OrderDate FROM Customers c INNER JOIN Orders o ON c.ID = o.CustomerID");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Table);
        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.Equal("c", selectStmt.Table.TableAlias);

        Assert.Single(selectStmt.Joins);
        var join = selectStmt.Joins[0];
        Assert.Equal(SqlJoinKind.Inner, join.JoinKind);
        Assert.Equal("Orders", join.Table.TableName);
        Assert.Equal("o", join.Table.TableAlias);
    }

    [Fact]
    public void Select_WithLeftJoin()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT c.CustomerName, o.OrderDate FROM Customers c LEFT JOIN Orders o ON c.ID = o.CustomerID");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Joins);
        Assert.Equal(SqlJoinKind.Left, selectStmt.Joins[0].JoinKind);
    }

    [Fact]
    public void Select_WithDistinct()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DISTINCT department FROM employees");

        var selectStmt = grammar.Create(node);

        Assert.True(selectStmt.IsDistinct);
        Assert.Single(selectStmt.Columns);
    }

    [Fact]
    public void Select_WithColumnAlias_AsKeyword()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT CustomerName AS Name FROM Customers");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var firstColumn = Assert.IsType<SqlColumn>(selectStmt.Columns[0]);
        Assert.Equal("CustomerName", firstColumn.ColumnName);
        Assert.Equal("Name", firstColumn.ColumnAlias);
    }

    [Fact]
    public void Select_WithTableAlias_AsKeyword()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT c.ID FROM Customers AS c");

        var selectStmt = grammar.Create(node);

        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.Equal("c", selectStmt.Table.TableAlias);
    }

    [Fact]
    public void Select_AggregateCount_Star()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT COUNT(*) FROM Customers");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var aggregate = Assert.IsType<SqlAggregate>(selectStmt.Columns[0]);
        Assert.Equal("COUNT", aggregate.AggregateName);
    }

    [Fact]
    public void Select_DotSeparatedIdentifiers()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT c.CustomerName FROM Customers c");

        var selectStmt = grammar.Create(node);

        var firstColumn = Assert.IsType<SqlColumn>(selectStmt.Columns[0]);
        Assert.Equal("CustomerName", firstColumn.ColumnName);
        Assert.Equal("c", firstColumn.TableName);
        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.Equal("c", selectStmt.Table.TableAlias);
    }

    [Fact]
    public void Select_WindowFrame_RangeIntervalDayPreceding_Parses()
    {
        // Issue #180: AnsiSQL grammar accepts SQL:2003 INTERVAL-literal bounds in
        // RANGE-mode window frames. The same SQL must produce a SqlWindowFrameBound
        // whose Offset is an Interval of magnitude 1, qualifier Day.
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT EventDate, " +
            "SUM(Amount) OVER (ORDER BY EventDate RANGE BETWEEN INTERVAL '1' DAY PRECEDING AND CURRENT ROW) AS rolling " +
            "FROM Events");

        var selectStmt = grammar.Create(node);
        var aggregate = Assert.IsType<SqlAggregate>(selectStmt.Columns[1]);
        Assert.True(aggregate.IsWindowFunction);

        var frame = aggregate.WindowSpecification!.Frame!;
        Assert.Equal(WindowFrameMode.Range, frame.Mode);
        Assert.Equal(WindowFrameBoundType.Preceding, frame.Start.Type);
        Assert.Equal(SqlWindowFrameBoundOffsetKind.Interval, frame.Start.Offset!.Kind);
        Assert.Equal(1L, frame.Start.Offset.Interval!.Magnitude);
        Assert.Equal(IntervalQualifier.Day, frame.Start.Offset.Interval.Qualifier);
        Assert.Equal(WindowFrameBoundType.CurrentRow, frame.End!.Type);
    }

    [Theory]
    [InlineData("YEAR", IntervalQualifier.Year)]
    [InlineData("MONTH", IntervalQualifier.Month)]
    [InlineData("DAY", IntervalQualifier.Day)]
    [InlineData("HOUR", IntervalQualifier.Hour)]
    [InlineData("MINUTE", IntervalQualifier.Minute)]
    [InlineData("SECOND", IntervalQualifier.Second)]
    public void Select_WindowFrame_RangeInterval_AllSingleFieldQualifiers_Parse(string keyword, IntervalQualifier expected)
    {
        // Issue #180: every SQL:2003 single-field qualifier (YEAR/MONTH/DAY/HOUR/MINUTE/SECOND)
        // must round-trip through the grammar to its IntervalQualifier enum value.
        TestGrammar grammar = new();
        var sql =
            "SELECT id, " +
            $"COUNT(*) OVER (ORDER BY ts RANGE BETWEEN INTERVAL '7' {keyword} PRECEDING AND CURRENT ROW) AS c " +
            "FROM samples";
        var node = GrammarParser.Parse(grammar, sql);

        var selectStmt = grammar.Create(node);
        var aggregate = Assert.IsType<SqlAggregate>(selectStmt.Columns[1]);
        var frame = aggregate.WindowSpecification!.Frame!;
        Assert.Equal(7L, frame.Start.Offset!.Interval!.Magnitude);
        Assert.Equal(expected, frame.Start.Offset.Interval.Qualifier);
    }
}
