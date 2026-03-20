using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.SQLServer.Tests;

public class TableHintOptTests
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
            SQLServer.TableName tableName = new(this, aliasOpt, id, tableHintOpt);
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
    }

    [Fact]
    public void Select_From_With_NoLock()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers WITH (NOLOCK)");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Table);
        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.Single(selectStmt.Table.TableHints);
        Assert.Equal("NOLOCK", selectStmt.Table.TableHints[0]);
    }

    [Fact]
    public void Select_From_With_NoLock_And_Alias()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers c WITH (NOLOCK)");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Table);
        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.Equal("c", selectStmt.Table.TableAlias);
        Assert.Single(selectStmt.Table.TableHints);
        Assert.Equal("NOLOCK", selectStmt.Table.TableHints[0]);
    }

    [Fact]
    public void Select_From_With_Multiple_Hints()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Orders WITH (ROWLOCK, UPDLOCK)");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Table);
        Assert.Equal("Orders", selectStmt.Table.TableName);
        Assert.Equal(2, selectStmt.Table.TableHints.Count);
        Assert.Equal("ROWLOCK", selectStmt.Table.TableHints[0]);
        Assert.Equal("UPDLOCK", selectStmt.Table.TableHints[1]);
    }

    [Fact]
    public void Select_From_With_HoldLock()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Products WITH (HOLDLOCK)");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Table);
        Assert.Single(selectStmt.Table.TableHints);
        Assert.Equal("HOLDLOCK", selectStmt.Table.TableHints[0]);
    }

    [Fact]
    public void Select_From_Without_Hints()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Table);
        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.Empty(selectStmt.Table.TableHints);
    }

    [Fact]
    public void Select_From_With_NoLock_BracketQuoted()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM [dbo].[Customers] WITH (NOLOCK)");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Table);
        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.Equal("dbo", selectStmt.Table.DatabaseName);
        Assert.Single(selectStmt.Table.TableHints);
        Assert.Equal("NOLOCK", selectStmt.Table.TableHints[0]);
    }

    [Fact]
    public void Select_From_With_NoLock_Where_Clause()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers WITH (NOLOCK) WHERE City = 'London'");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Table);
        Assert.Single(selectStmt.Table.TableHints);
        Assert.Equal("NOLOCK", selectStmt.Table.TableHints[0]);
        Assert.NotNull(selectStmt.WhereClause);
    }

    [Fact]
    public void Select_Join_With_NoLock()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Orders o WITH (NOLOCK) INNER JOIN Customers c WITH (NOLOCK) ON o.CustomerID = c.CustomerID");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Table);
        Assert.Equal("Orders", selectStmt.Table.TableName);
        Assert.Single(selectStmt.Table.TableHints);
        Assert.Equal("NOLOCK", selectStmt.Table.TableHints[0]);

        Assert.Single(selectStmt.Joins);
        Assert.Equal("Customers", selectStmt.Joins[0].Table.TableName);
        Assert.Equal("c", selectStmt.Joins[0].Table.TableAlias);
        Assert.Single(selectStmt.Joins[0].Table.TableHints);
        Assert.Equal("NOLOCK", selectStmt.Joins[0].Table.TableHints[0]);
    }

    [Fact]
    public void Select_From_With_TabLock()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Orders WITH (TABLOCK)");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Table);
        Assert.Single(selectStmt.Table.TableHints);
        Assert.Equal("TABLOCK", selectStmt.Table.TableHints[0]);
    }

    [Fact]
    public void Select_From_With_NoLock_CaseInsensitive()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "select * from Customers with (nolock)");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Table);
        Assert.Single(selectStmt.Table.TableHints);
        Assert.Equal("NOLOCK", selectStmt.Table.TableHints[0]);
    }

    [Fact]
    public void Select_Top_With_NoLock()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT TOP 10 * FROM Customers WITH (NOLOCK)");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Top);
        Assert.Equal(10, selectStmt.Top.Count.Value);
        Assert.NotNull(selectStmt.Table);
        Assert.Single(selectStmt.Table.TableHints);
        Assert.Equal("NOLOCK", selectStmt.Table.TableHints[0]);
    }

    [Fact]
    public void Select_From_With_Three_Hints()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Orders WITH (ROWLOCK, UPDLOCK, HOLDLOCK)");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Table);
        Assert.Equal(3, selectStmt.Table.TableHints.Count);
        Assert.Equal("ROWLOCK", selectStmt.Table.TableHints[0]);
        Assert.Equal("UPDLOCK", selectStmt.Table.TableHints[1]);
        Assert.Equal("HOLDLOCK", selectStmt.Table.TableHints[2]);
    }
}
