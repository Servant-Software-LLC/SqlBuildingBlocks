using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class SimpleIdListTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            SimpleIdList simpleIdList = new(simpleId, this);

            Root = simpleIdList;
        }

        public IEnumerable<string> Create(ParseTreeNode node) =>
            ((SimpleIdList)Root).Create(node);
    }

    [Fact]
    public void Create_WithSingleId_ReturnsOneItem()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "ColumnA");

        // Act
        var result = grammar.Create(node).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("ColumnA", result[0]);
    }

    [Fact]
    public void Create_WithMultipleIds_ReturnsAllItemsInOrder()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "ColumnA, ColumnB, ColumnC");

        // Act
        var result = grammar.Create(node).ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("ColumnA", result[0]);
        Assert.Equal("ColumnB", result[1]);
        Assert.Equal("ColumnC", result[2]);
    }

    [Fact]
    public void Create_WithBracketedIds_ReturnsUnbracketedStrings()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "[FirstName], [LastName]");

        // Act
        var result = grammar.Create(node).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("FirstName", result[0]);
        Assert.Equal("LastName", result[1]);
    }

    [Fact]
    public void Create_WithMixedBracketedAndBareIds_ReturnsCorrectStrings()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "Id, [UserName], Email");

        // Act
        var result = grammar.Create(node).ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Id", result[0]);
        Assert.Equal("UserName", result[1]);
        Assert.Equal("Email", result[2]);
    }

    [Fact]
    public void Parse_WithEmptyInput_HasErrors()
    {
        // Arrange
        TestGrammar grammar = new();

        // Act
        var parseTree = GrammarParser.ParseTree(grammar, string.Empty);

        // Assert: SimpleIdList uses MakePlusRule, requiring at least one element
        Assert.True(parseTree.HasErrors());
    }

    [Fact]
    public void Parse_WithTrailingComma_HasErrors()
    {
        // Arrange
        TestGrammar grammar = new();

        // Act
        var parseTree = GrammarParser.ParseTree(grammar, "ColumnA,");

        // Assert
        Assert.True(parseTree.HasErrors());
    }

    [Fact]
    public void Constructor_WithNullSimpleId_ThrowsArgumentNullException()
    {
        // Arrange
        var grammar = new Grammar();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new SimpleIdList(null!, grammar));
    }
}
