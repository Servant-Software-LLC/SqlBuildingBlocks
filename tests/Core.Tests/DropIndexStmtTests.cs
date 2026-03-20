using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class DropIndexStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            Id id = new(this, simpleId);

            DropIndexStmt dropIndexStmt = new(this, id);

            Root = dropIndexStmt;
        }

        public virtual SqlDropIndexDefinition Create(ParseTreeNode node) =>
            ((DropIndexStmt)Root).Create(node);
    }

    [Fact]
    public void DropIndex_Simple()
    {
        //Setup
        const string sql = "DROP INDEX idx_orders_customer";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.False(result.IfExists);
        Assert.Equal("idx_orders_customer", result.IndexName);
    }

    [Fact]
    public void DropIndex_IfExists()
    {
        //Setup
        const string sql = "DROP INDEX IF EXISTS idx_old";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.True(result.IfExists);
        Assert.Equal("idx_old", result.IndexName);
    }
}
