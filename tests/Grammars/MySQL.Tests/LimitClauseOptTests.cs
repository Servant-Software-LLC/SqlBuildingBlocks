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

    [Fact]
    public void CanCreateLimitZero()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "LIMIT 0");

        var limitOffset = grammar.Create(node);

        Assert.Equal(0, limitOffset.RowCount.Value);
        Assert.Equal(0, limitOffset.RowOffset.Value);
    }

    [Fact]
    public void CanCreateLimitWithZeroOffsetComma()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "LIMIT 0, 10");

        var limitOffset = grammar.Create(node);

        Assert.Equal(10, limitOffset.RowCount.Value);
        Assert.Equal(0, limitOffset.RowOffset.Value);
    }

    [Fact]
    public void CanCreateLimitAndOffsetWithComma_Parameters()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "LIMIT @offset, @count");

        var limitOffset = grammar.Create(node);

        Assert.NotNull(limitOffset.RowOffset.Parameter);
        Assert.Equal("offset", limitOffset.RowOffset.Parameter.Name);
        Assert.NotNull(limitOffset.RowCount.Parameter);
        Assert.Equal("count", limitOffset.RowCount.Parameter.Name);
    }

    [Fact]
    public void CanCreateLimitAndOffsetWithKeyword_Parameters()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "LIMIT @count OFFSET @offset");

        var limitOffset = grammar.Create(node);

        Assert.NotNull(limitOffset.RowCount.Parameter);
        Assert.Equal("count", limitOffset.RowCount.Parameter.Name);
        Assert.NotNull(limitOffset.RowOffset.Parameter);
        Assert.Equal("offset", limitOffset.RowOffset.Parameter.Name);
    }

    [Fact]
    public void CanCreateLimitWithLargeValues()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "LIMIT 1000, 500");

        var limitOffset = grammar.Create(node);

        Assert.Equal(500, limitOffset.RowCount.Value);
        Assert.Equal(1000, limitOffset.RowOffset.Value);
    }

    [Fact]
    public void CommaAndOffsetKeyword_ProduceSameResult()
    {
        TestGrammar grammar = new();

        var commaNode = GrammarParser.Parse(grammar, "LIMIT 20, 10");
        var commaResult = grammar.Create(commaNode);

        var offsetNode = GrammarParser.Parse(grammar, "LIMIT 10 OFFSET 20");
        var offsetResult = grammar.Create(offsetNode);

        // Both forms should produce the same SqlLimitOffset values
        Assert.Equal(commaResult.RowCount.Value, offsetResult.RowCount.Value);
        Assert.Equal(commaResult.RowOffset.Value, offsetResult.RowOffset.Value);
    }

}
