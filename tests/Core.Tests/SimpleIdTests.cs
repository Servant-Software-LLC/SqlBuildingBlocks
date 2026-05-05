using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class SimpleIdTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);

            Root = simpleId;
        }

        public string Create(ParseTreeNode node) => ((SimpleId)Root).Create(node);
    }

    [Fact]
    public void Create_WithBareIdentifier_ReturnsIdentifierString()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "MyColumn");

        // Act
        var result = grammar.Create(node);

        // Assert
        Assert.Equal("MyColumn", result);
    }

    [Fact]
    public void Create_WithBracketedIdentifier_ReturnsUnbracketedIdentifierString()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "[MyColumn]");

        // Act
        var result = grammar.Create(node);

        // Assert
        Assert.Equal("MyColumn", result);
    }

    [Fact]
    public void Create_WithBracketedReservedWord_ReturnsUnbracketedString()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "[Order]");

        // Act
        var result = grammar.Create(node);

        // Assert
        Assert.Equal("Order", result);
    }

    [Fact]
    public void Create_WithSingleCharacterIdentifier_ReturnsString()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "x");

        // Act
        var result = grammar.Create(node);

        // Assert
        Assert.Equal("x", result);
    }

    [Fact]
    public void Create_WithIdentifierContainingUnderscoresAndDigits_ReturnsString()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "col_123_name");

        // Act
        var result = grammar.Create(node);

        // Assert
        Assert.Equal("col_123_name", result);
    }

    [Fact]
    public void Parse_WithEmptyInput_HasErrors()
    {
        // Arrange
        TestGrammar grammar = new();

        // Act
        var parseTree = GrammarParser.ParseTree(grammar, string.Empty);

        // Assert
        Assert.True(parseTree.HasErrors());
    }
}
