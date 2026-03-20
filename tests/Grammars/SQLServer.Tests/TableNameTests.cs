using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.SQLServer.Tests;

public class TableNameTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SqlBuildingBlocks.Grammars.SQLServer.SimpleId simpleId = new(this);

            var aliasOpt = new AliasOpt(this, simpleId);
            var id = new Id(this, simpleId);
            var tableName = new TableName(this, aliasOpt, id);

            Root = tableName;
        }

        public virtual SqlTable Create(ParseTreeNode tableId) => ((TableName)Root).Create(tableId);
    }

    [Fact]
    public void TableName_WithTableAndAlias_ReturnsCorrectSqlTable()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "\"locations\" AS \"l\"");
        var table = grammar.Create(node);

        Assert.Null(table.DatabaseName);
        Assert.Equal("locations", table.TableName);
        Assert.Equal("l", table.TableAlias);
    }

    [Fact]
    public void TableName_BracketQuoted_WithAlias()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "[locations] AS [l]");
        var table = grammar.Create(node);

        Assert.Null(table.DatabaseName);
        Assert.Equal("locations", table.TableName);
        Assert.Equal("l", table.TableAlias);
    }

    [Fact]
    public void TableName_BracketQuoted_NoAlias()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "[Customers]");
        var table = grammar.Create(node);

        Assert.Null(table.DatabaseName);
        Assert.Equal("Customers", table.TableName);
        Assert.Null(table.TableAlias);
    }

    [Fact]
    public void TableName_BracketQuoted_TwoPartName()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "[dbo].[Customers]");
        var table = grammar.Create(node);

        Assert.Equal("dbo", table.DatabaseName);
        Assert.Equal("Customers", table.TableName);
    }

    [Fact]
    public void TableName_BracketQuoted_ReservedWord()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "[Order]");
        var table = grammar.Create(node);

        Assert.Null(table.DatabaseName);
        Assert.Equal("Order", table.TableName);
    }
}
