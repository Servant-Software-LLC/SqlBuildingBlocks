using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class CreateIndexStmtTests
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
            WhereClauseOpt whereClauseOpt = new(this, selectStmt.JoinChainOpt.Expr);

            CreateIndexStmt createIndexStmt = new(this, id, whereClauseOpt);

            Root = createIndexStmt;
        }

        public virtual SqlCreateIndexDefinition Create(ParseTreeNode node) =>
            ((CreateIndexStmt)Root).Create(node);
    }

    [Fact]
    public void CreateIndex_Simple()
    {
        //Setup
        const string sql = "CREATE INDEX idx_orders_customer ON orders (customer_id)";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.False(result.IsUnique);
        Assert.Equal("idx_orders_customer", result.IndexName);
        Assert.NotNull(result.Table);
        Assert.Equal("orders", result.Table!.TableName);
        Assert.Single(result.Columns);
        Assert.Equal("customer_id", result.Columns[0].ColumnName);
        Assert.False(result.Columns[0].Descending);
        Assert.Null(result.WhereClause);
    }

    [Fact]
    public void CreateIndex_Unique()
    {
        //Setup
        const string sql = "CREATE UNIQUE INDEX idx_email ON customers (email)";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.True(result.IsUnique);
        Assert.Equal("idx_email", result.IndexName);
        Assert.Equal("customers", result.Table!.TableName);
        Assert.Single(result.Columns);
        Assert.Equal("email", result.Columns[0].ColumnName);
    }

    [Fact]
    public void CreateIndex_MultipleColumns()
    {
        //Setup
        const string sql = "CREATE INDEX idx_composite ON orders (customer_id, order_date DESC)";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.False(result.IsUnique);
        Assert.Equal("idx_composite", result.IndexName);
        Assert.Equal(2, result.Columns.Count);
        Assert.Equal("customer_id", result.Columns[0].ColumnName);
        Assert.False(result.Columns[0].Descending);
        Assert.Equal("order_date", result.Columns[1].ColumnName);
        Assert.True(result.Columns[1].Descending);
    }

    [Fact]
    public void CreateIndex_WithAscDesc()
    {
        //Setup
        const string sql = "CREATE INDEX idx_sort ON products (name ASC, price DESC)";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(2, result.Columns.Count);
        Assert.Equal("name", result.Columns[0].ColumnName);
        Assert.False(result.Columns[0].Descending);
        Assert.Equal("price", result.Columns[1].ColumnName);
        Assert.True(result.Columns[1].Descending);
    }

    [Fact]
    public void CreateIndex_Partial_WithWhere()
    {
        //Setup
        const string sql = "CREATE INDEX idx_active ON customers (status) WHERE status = 'active'";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.False(result.IsUnique);
        Assert.Equal("idx_active", result.IndexName);
        Assert.Equal("customers", result.Table!.TableName);
        Assert.Single(result.Columns);
        Assert.Equal("status", result.Columns[0].ColumnName);
        Assert.NotNull(result.WhereClause);
    }
}
