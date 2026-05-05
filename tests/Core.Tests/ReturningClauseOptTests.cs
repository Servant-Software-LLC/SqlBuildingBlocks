#nullable enable
using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class ReturningClauseOptTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            Id id = new(this, simpleId);
            ReturningClauseOpt returningClauseOpt = new(this, id);

            Root = returningClauseOpt;
        }

        public SqlReturning? Create(ParseTreeNode node) =>
            ((ReturningClauseOpt)Root).Create(node);
    }

    [Fact]
    public void Create_WithReturningInteger_ReturnsSqlReturningWithInt()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "RETURNING 1");

        // Act
        var returning = grammar.Create(node);

        // Assert
        Assert.NotNull(returning);
        Assert.Equal(1, returning!.Int);
        Assert.Null(returning.Column);
    }

    [Fact]
    public void Create_WithReturningColumnName_ReturnsSqlReturningWithColumn()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "RETURNING Id");

        // Act
        var returning = grammar.Create(node);

        // Assert
        Assert.NotNull(returning);
        Assert.NotNull(returning!.Column);
        Assert.Equal("Id", returning.Column!.ColumnName);
        Assert.Null(returning.Int);
    }

    [Fact]
    public void Create_WithReturningQualifiedColumn_ReturnsSqlReturningWithQualifiedColumn()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "RETURNING employees.Id");

        // Act
        var returning = grammar.Create(node);

        // Assert
        Assert.NotNull(returning);
        Assert.NotNull(returning!.Column);
        Assert.Equal("Id", returning.Column!.ColumnName);
        Assert.Null(returning.Int);
    }

    [Fact]
    public void Create_WithEmptyInput_ReturnsNull()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, string.Empty);

        // Act
        var returning = grammar.Create(node);

        // Assert
        Assert.Null(returning);
    }

    [Fact]
    public void Create_WithReturningLargeInteger_ReturnsSqlReturningWithInt()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "RETURNING 42");

        // Act
        var returning = grammar.Create(node);

        // Assert
        Assert.NotNull(returning);
        Assert.Equal(42, returning!.Int);
    }

    [Fact]
    public void Constructor_WithNullId_ThrowsArgumentNullException()
    {
        // Arrange
        var grammar = new Grammar();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new ReturningClauseOpt(grammar, null!));
    }
}
