using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.SQLServer.Tests;

public class TopClauseOptTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar() : base(false)  //SQL is case insensitive
        {
            SQLServer.SimpleId simpleId = new(this);

            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableHintOpt tableHintOpt = new(this);
            TableName tableName = new(this, aliasOpt, id, tableHintOpt);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = selectStmt;
        }

        public SqlSelectDefinition Create(ParseTreeNode selectStmt) =>
            ((SelectStmt)Root).Create(selectStmt);

        public SqlSelectDefinition Create(ParseTreeNode selectStmt, IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider) =>
            ((SelectStmt)Root).Create(selectStmt, databaseConnectionProvider, tableSchemaProvider);
    }

    [Fact]
    public void Select_Top_N()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT TOP 10 * FROM Customers");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Top);
        Assert.Equal(10, selectStmt.Top.Count.Value);
        Assert.False(selectStmt.Top.Percent);
        Assert.False(selectStmt.Top.WithTies);
        Assert.Equal("Customers", selectStmt.Table.TableName);
    }

    [Fact]
    public void Select_Top_N_With_Columns()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT TOP 5 CustomerName, City FROM Customers");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Top);
        Assert.Equal(5, selectStmt.Top.Count.Value);
        Assert.False(selectStmt.Top.Percent);
        Assert.Equal(2, selectStmt.Columns.Count);
    }

    [Fact]
    public void Select_Top_N_Percent()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT TOP 10 PERCENT * FROM Customers");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Top);
        Assert.Equal(10, selectStmt.Top.Count.Value);
        Assert.True(selectStmt.Top.Percent);
        Assert.False(selectStmt.Top.WithTies);
    }

    [Fact]
    public void Select_Top_N_WithTies()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT TOP 10 WITH TIES * FROM Customers ORDER BY Price");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Top);
        Assert.Equal(10, selectStmt.Top.Count.Value);
        Assert.False(selectStmt.Top.Percent);
        Assert.True(selectStmt.Top.WithTies);
    }

    [Fact]
    public void Select_Top_N_Percent_WithTies()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT TOP 10 PERCENT WITH TIES * FROM Customers ORDER BY Price");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Top);
        Assert.Equal(10, selectStmt.Top.Count.Value);
        Assert.True(selectStmt.Top.Percent);
        Assert.True(selectStmt.Top.WithTies);
    }

    [Fact]
    public void Select_Top_Parenthesized_Expression()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT TOP (10) * FROM Customers");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Top);
        Assert.Equal(10, selectStmt.Top.Count.Value);
        Assert.False(selectStmt.Top.Percent);
        Assert.False(selectStmt.Top.WithTies);
    }

    [Fact]
    public void Select_Top_Parenthesized_Percent()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT TOP (25) PERCENT * FROM Products");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Top);
        Assert.Equal(25, selectStmt.Top.Count.Value);
        Assert.True(selectStmt.Top.Percent);
        Assert.False(selectStmt.Top.WithTies);
    }

    [Fact]
    public void Select_Top_With_Parameter()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT TOP (@n) * FROM Customers");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Top);
        Assert.NotNull(selectStmt.Top.Count.Parameter);
        Assert.Equal("n", selectStmt.Top.Count.Parameter.Name);
        Assert.False(selectStmt.Top.Percent);
        Assert.False(selectStmt.Top.WithTies);
    }

    [Fact]
    public void Select_Without_Top()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers");

        var selectStmt = grammar.Create(node);

        Assert.Null(selectStmt.Top);
    }

    [Fact]
    public void Select_Top_With_Where()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT TOP 5 [CustomerName] FROM [Customers] WHERE [City] = 'London'");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Top);
        Assert.Equal(5, selectStmt.Top.Count.Value);
        Assert.NotNull(selectStmt.WhereClause);
    }

    [Fact]
    public void Select_Top_With_BracketQuotedIdentifiers()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT TOP 10 [c].[CustomerName], [c].[City] FROM [Customers] [c]");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.NotNull(selectStmt.Top);
        Assert.Equal(10, selectStmt.Top.Count.Value);
        Assert.Equal(2, selectStmt.Columns.Count);
        Assert.Equal("Customers", selectStmt.Table.TableName);
    }

    [Fact]
    public void Select_Top_CaseInsensitive()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "select top 10 percent * from Customers");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Top);
        Assert.Equal(10, selectStmt.Top.Count.Value);
        Assert.True(selectStmt.Top.Percent);
    }

    [Fact]
    public void Select_Distinct_Top()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DISTINCT TOP 5 CustomerName FROM Customers");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Top);
        Assert.Equal(5, selectStmt.Top.Count.Value);
    }
}
