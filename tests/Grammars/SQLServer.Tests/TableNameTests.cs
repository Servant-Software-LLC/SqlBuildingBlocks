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

}
