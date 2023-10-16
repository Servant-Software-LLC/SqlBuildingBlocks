using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class LiteralValueTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            LiteralValue literalValue = new(this);

            Root = literalValue;
        }

        public virtual SqlLiteralValue Create(ParseTreeNode parseTreeNode) => ((LiteralValue)Root).Create(parseTreeNode);
    }

    [Fact]
    public void CanCreateStringLiteralValue()
    {
        // Arrange
        var input = "'Hello World'";

        // Act
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, input);
        var literalValue = grammar.Create(node);

        // Assert
        Assert.Equal("Hello World", literalValue.String);
        Assert.Null(literalValue.Int);
    }

    [Fact]
    public void CanCreateIntegerLiteralValue()
    {
        // Arrange
        var input = "12345";

        // Act
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, input);
        var literalValue = grammar.Create(node);

        // Assert
        Assert.Equal(12345, literalValue.Int);
        Assert.Null(literalValue.String);
    }

    [Fact]
    public void ThrowsExceptionForInvalidLiteralValueType()
    {
        // Arrange
        var input = "NotALiteral";

        // Act
        TestGrammar grammar = new();
        var parseTree = GrammarParser.ParseTree(grammar, input);

        // Assert
        Assert.True(parseTree.HasErrors());
    }

    [Fact]
    public void NullValue()
    {
        // Arrange
        var input = "NULL";

        // Act
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, input);
        var literalValue = grammar.Create(node);

        // Assert
        Assert.Null(literalValue.Value);
    }

    [Fact]
    public void StringWithNull()
    {
        // Arrange
        var input = "'NULL'";

        // Act
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, input);
        var literalValue = grammar.Create(node);

        // Assert
        Assert.Equal("NULL", literalValue.Value);
    }

}
