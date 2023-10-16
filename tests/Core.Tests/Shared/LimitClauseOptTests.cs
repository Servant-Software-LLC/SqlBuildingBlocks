using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.Shared;
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
    public void CanCreateOffsetOnly()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "OFFSET 10");

        var limitOffset = grammar.Create(node);

        Assert.Equal(0, limitOffset.RowCount.Value);
        Assert.Equal(10, limitOffset.RowOffset.Value);
    }

    [Fact]
    public void CanCreateLimitAndOffset()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "LIMIT 5 OFFSET 10");

        var limitOffset = grammar.Create(node);

        Assert.Equal(5, limitOffset.RowCount.Value);
        Assert.Equal(10, limitOffset.RowOffset.Value);
    }

    [Fact]
    public void CanCreateEmptyLimitOffset()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "");

        var limitOffset = grammar.Create(node);

        Assert.Equal(0, limitOffset.RowCount.Value);
        Assert.Equal(0, limitOffset.RowOffset.Value);
    }

}
