using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.Grammars.MySQL;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.Shared;

public class LimitClauseOptTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            LimitOffsetClauseOpt limitOffsetClauseOpt = new(this);

            Root = limitOffsetClauseOpt;
        }

        public virtual SqlLimitOffset Create(ParseTreeNode limitOffsetClauseOpt) =>
            ((LimitOffsetClauseOpt)Root).Create(limitOffsetClauseOpt);
    }

    [Fact]
    public void CanCreateLimitOnly()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "LIMIT 5");

        var limitOffset = grammar.Create(node);

        Assert.Equal(5, limitOffset.RowCount.Value);
        Assert.Equal(0, limitOffset.RowOffset.Value);
    }

    [Fact]
    public void CanCreateLimitAndOffsetWithComma()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "LIMIT 5, 10");

        var limitOffset = grammar.Create(node);

        Assert.Equal(10, limitOffset.RowCount.Value);
        Assert.Equal(5, limitOffset.RowOffset.Value);
    }

    [Fact]
    public void CanCreateLimitAndOffsetWithOffsetKeyword()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "LIMIT 15 OFFSET 10");

        var limitOffset = grammar.Create(node);

        Assert.Equal(15, limitOffset.RowCount.Value);
        Assert.Equal(10, limitOffset.RowOffset.Value);
    }

    [Fact]
    public void Select_WithSimpleLimit_Parameter()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"LIMIT @__p_0");

        var limitOffset = grammar.Create(node);

        Assert.NotNull(limitOffset.RowCount.Parameter);
        Assert.Equal("__p_0", limitOffset.RowCount.Parameter.Name);
        Assert.Null(limitOffset.RowOffset.Parameter);
    }

}
