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

    [Theory]
    [InlineData("TRUE")]
    [InlineData("True")]
    [InlineData("T")]
    [InlineData("t")]
    [InlineData("yes")]
    [InlineData("on")]
    public void CanCreateTrueBooleanLiteralValue(string input)
    {
        // Arrange

        // Act
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, input);
        var literalValue = grammar.Create(node);

        // Assert
        Assert.Equal(true, literalValue.Boolean);
        Assert.Equal("TRUE", literalValue.ToString());
    }

    [Theory]
    [InlineData("FALSE")]
    [InlineData("False")]
    [InlineData("F")]
    [InlineData("f")]
    [InlineData("no")]
    [InlineData("off")]
    public void CanCreateFalseBooleanLiteralValue(string input)
    {
        // Arrange

        // Act
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, input);
        var literalValue = grammar.Create(node);

        // Assert
        Assert.Equal(false, literalValue.Boolean);
        Assert.Equal("FALSE", literalValue.ToString());
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
