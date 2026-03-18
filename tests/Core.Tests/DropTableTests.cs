using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class DropTableTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            Id id = new(this, simpleId);

            DropTableStmt dropTableStmt = new(this, id);

            Root = dropTableStmt;
        }

        public virtual SqlDropTableDefinition Create(ParseTreeNode dropTableStmt) =>
            ((DropTableStmt)Root).Create(dropTableStmt);
    }

    [Fact]
    public void DropTable_Simple()
    {
        //Setup
        const string sql = "DROP TABLE orders";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.False(result.IfExists);
        Assert.Single(result.Tables);
        Assert.Equal("orders", result.Tables[0].TableName);
        Assert.Null(result.Tables[0].DatabaseName);
    }

    [Fact]
    public void DropTable_IfExists()
    {
        //Setup
        const string sql = "DROP TABLE IF EXISTS temp_data";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.True(result.IfExists);
        Assert.Single(result.Tables);
        Assert.Equal("temp_data", result.Tables[0].TableName);
    }

    [Fact]
    public void DropTable_MultiTable()
    {
        //Setup
        const string sql = "DROP TABLE orders, customers, products";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.False(result.IfExists);
        Assert.Equal(3, result.Tables.Count);
        Assert.Equal("orders", result.Tables[0].TableName);
        Assert.Equal("customers", result.Tables[1].TableName);
        Assert.Equal("products", result.Tables[2].TableName);
    }

    [Fact]
    public void DropTable_MultiTable_IfExists()
    {
        //Setup
        const string sql = "DROP TABLE IF EXISTS orders, temp_data";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.True(result.IfExists);
        Assert.Equal(2, result.Tables.Count);
        Assert.Equal("orders", result.Tables[0].TableName);
        Assert.Equal("temp_data", result.Tables[1].TableName);
    }

    [Fact]
    public void DropTable_QualifiedName()
    {
        //Setup
        const string sql = "DROP TABLE mydb.orders";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.False(result.IfExists);
        Assert.Single(result.Tables);
        Assert.Equal("mydb", result.Tables[0].DatabaseName);
        Assert.Equal("orders", result.Tables[0].TableName);
    }
}
