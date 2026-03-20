using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.SQLServer.Tests;

public class OptionClauseOptTests
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
    public void Select_Option_Recompile()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers OPTION (RECOMPILE)");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Table);
        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.Single(selectStmt.QueryHints);
        Assert.Equal("RECOMPILE", selectStmt.QueryHints[0]);
    }

    [Fact]
    public void Select_Option_MaxDop()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers OPTION (MAXDOP 1)");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.QueryHints);
        Assert.Equal("MAXDOP 1", selectStmt.QueryHints[0]);
    }

    [Fact]
    public void Select_Option_LoopJoin()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Orders INNER JOIN Customers ON Orders.CustomerID = Customers.CustomerID OPTION (LOOP JOIN)");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.QueryHints);
        Assert.Equal("LOOP JOIN", selectStmt.QueryHints[0]);
    }

    [Fact]
    public void Select_Option_Multiple_Hints()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers OPTION (RECOMPILE, MAXDOP 1)");

        var selectStmt = grammar.Create(node);

        Assert.Equal(2, selectStmt.QueryHints.Count);
        Assert.Equal("RECOMPILE", selectStmt.QueryHints[0]);
        Assert.Equal("MAXDOP 1", selectStmt.QueryHints[1]);
    }

    [Fact]
    public void Select_Option_HashGroup()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT City, COUNT(*) FROM Customers GROUP BY City OPTION (HASH GROUP)");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.QueryHints);
        Assert.Equal("HASH GROUP", selectStmt.QueryHints[0]);
    }

    [Fact]
    public void Select_Option_ForceOrder()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers OPTION (FORCE ORDER)");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.QueryHints);
        Assert.Equal("FORCE ORDER", selectStmt.QueryHints[0]);
    }

    [Fact]
    public void Select_Option_Fast()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers OPTION (FAST 100)");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.QueryHints);
        Assert.Equal("FAST 100", selectStmt.QueryHints[0]);
    }

    [Fact]
    public void Select_Without_Option()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers");

        var selectStmt = grammar.Create(node);

        Assert.Empty(selectStmt.QueryHints);
    }

    [Fact]
    public void Select_Option_With_OrderBy()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers ORDER BY CustomerName OPTION (RECOMPILE)");

        var selectStmt = grammar.Create(node);

        Assert.NotEmpty(selectStmt.OrderBy);
        Assert.Single(selectStmt.QueryHints);
        Assert.Equal("RECOMPILE", selectStmt.QueryHints[0]);
    }

    [Fact]
    public void Select_NoLock_And_Option()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers WITH (NOLOCK) OPTION (RECOMPILE)");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Table);
        Assert.Single(selectStmt.Table.TableHints);
        Assert.Equal("NOLOCK", selectStmt.Table.TableHints[0]);
        Assert.Single(selectStmt.QueryHints);
        Assert.Equal("RECOMPILE", selectStmt.QueryHints[0]);
    }

    [Fact]
    public void Select_Option_ExpandViews()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers OPTION (EXPAND VIEWS)");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.QueryHints);
        Assert.Equal("EXPAND VIEWS", selectStmt.QueryHints[0]);
    }

    [Fact]
    public void Select_Option_OptimizeForUnknown()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers WHERE City = @city OPTION (OPTIMIZE FOR UNKNOWN)");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.QueryHints);
        Assert.Equal("OPTIMIZE FOR UNKNOWN", selectStmt.QueryHints[0]);
    }

    [Fact]
    public void Select_Option_CaseInsensitive()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "select * from Customers option (recompile)");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.QueryHints);
        Assert.Equal("RECOMPILE", selectStmt.QueryHints[0]);
    }
}
