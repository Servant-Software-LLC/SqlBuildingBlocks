using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class AliasOptTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            var simpleId = new SimpleId(this);
            var aliasOpt = new AliasOpt(this, simpleId);

            Root = aliasOpt;
        }


    }

    [Fact]
    public void AliasOpt_WithoutAlias_ParsesCorrectly()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "");

        // Apply your assertions here to verify the correct behavior
        // For example, if AliasOpt should generate an empty node when no alias is provided:
        Assert.True(node.ChildNodes.Count == 0);
    }

    [Fact]
    public void AliasOpt_WithAliasWithoutAs_ParsesCorrectly()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "Alias");

        // Apply your assertions here to verify the correct behavior
        // For example, if AliasOpt should generate a single child node when an alias is provided without 'AS':
        Assert.True(node.ChildNodes.Count == 1);
        Assert.Equal("Alias", node.ChildNodes[0].ChildNodes[0].Token.Text);
    }

    [Fact]
    public void AliasOpt_WithAliasWithAs_ParsesCorrectly()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "AS Alias");

        // Apply your assertions here to verify the correct behavior
        // For example, if AliasOpt should generate two child nodes when an alias is provided with 'AS':
        Assert.True(node.ChildNodes.Count == 1);
        Assert.Equal("Alias", node.ChildNodes[0].ChildNodes[0].Token.Text);
    }
}
