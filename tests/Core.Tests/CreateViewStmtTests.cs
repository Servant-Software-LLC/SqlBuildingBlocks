using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class CreateViewStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            Id id = new(this, simpleId);
            SelectStmt selectStmt = new(this, id);
            FuncCall funcCall = new(this, selectStmt.JoinChainOpt.Expr.Id, selectStmt.JoinChainOpt.Expr);
            selectStmt.Expr.InitializeRule(selectStmt, funcCall);

            CreateViewStmt createViewStmt = new(this, id, selectStmt);

            Root = createViewStmt;
        }

        public virtual SqlCreateViewDefinition Create(ParseTreeNode node) =>
            ((CreateViewStmt)Root).Create(node);
    }

    [Fact]
    public void CreateView_Simple()
    {
        //Setup
        const string sql = "CREATE VIEW active_customers AS SELECT * FROM customers WHERE status = 'active'";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.False(result.OrReplace);
        Assert.NotNull(result.View);
        Assert.Equal("active_customers", result.View!.TableName);
        Assert.NotNull(result.AsSelect);
    }

    [Fact]
    public void CreateView_OrReplace()
    {
        //Setup
        const string sql = "CREATE OR REPLACE VIEW monthly_sales AS SELECT month, SUM(amount) FROM orders GROUP BY month";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.True(result.OrReplace);
        Assert.NotNull(result.View);
        Assert.Equal("monthly_sales", result.View!.TableName);
        Assert.NotNull(result.AsSelect);
    }

    [Fact]
    public void CreateView_QualifiedName()
    {
        //Setup
        const string sql = "CREATE VIEW mydb.customer_view AS SELECT id, name FROM customers";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.False(result.OrReplace);
        Assert.NotNull(result.View);
        Assert.Equal("mydb", result.View!.DatabaseName);
        Assert.Equal("customer_view", result.View.TableName);
        Assert.NotNull(result.AsSelect);
    }

    [Fact]
    public void CreateView_WithJoin()
    {
        //Setup
        const string sql = "CREATE VIEW order_details AS SELECT o.id, c.name FROM orders o JOIN customers c ON o.customer_id = c.id";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.False(result.OrReplace);
        Assert.Equal("order_details", result.View!.TableName);
        Assert.NotNull(result.AsSelect);
    }
}
