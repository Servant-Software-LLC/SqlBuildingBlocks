using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class TableNameTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            var simpleId = new SimpleId(this);
            var aliasOpt = new AliasOpt(this, simpleId);
            var id = new Id(this, simpleId);
            var tableName = new TableName(this, aliasOpt, id);

            Root = tableName;
        }

        public virtual SqlTable Create(ParseTreeNode tableId) => ((TableName)Root).Create(tableId);
    }

    [Fact]
    public void TableName_WithDatabaseAndTableAndAlias_ReturnsCorrectSqlTable()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "[Database].[Table] Alias");
        var table = grammar.Create(node);

        Assert.Equal("Database", table.DatabaseName);
        Assert.Equal("Table", table.TableName);
        Assert.Equal("Alias", table.TableAlias);
    }

    [Fact]
    public void TableName_WithDatabaseAndTable_ReturnsCorrectSqlTable()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "[Database].[Table]");
        var table = grammar.Create(node);

        Assert.Equal("Database", table.DatabaseName);
        Assert.Equal("Table", table.TableName);
        Assert.Null(table.TableAlias);
    }

    [Fact]
    public void TableName_WithTableAndAlias_ReturnsCorrectSqlTable()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "[Table] [Alias]");
        var table = grammar.Create(node);

        Assert.Null(table.DatabaseName);
        Assert.Equal("Table", table.TableName);
        Assert.Equal("Alias", table.TableAlias);
    }

    [Fact]
    public void TableName_WithOnlyTable_ReturnsCorrectSqlTable()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "[Table]");
        var table = grammar.Create(node);

        Assert.Null(table.DatabaseName);
        Assert.Equal("Table", table.TableName);
        Assert.Null(table.TableAlias);
    }
}
