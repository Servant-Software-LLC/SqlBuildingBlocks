using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class CommentTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar(bool registerComments)
        {
            if (registerComments)
                Comment.Register(this);

            SimpleId simpleId = new(this);

            Root = simpleId;
        }
    }

    [Fact]
    public void Register_AddsTwoNonGrammarTerminals()
    {
        // Arrange
        TestGrammar grammar = new(registerComments: true);

        // Act
        var nonGrammarTerminalNames = grammar.NonGrammarTerminals
            .Select(t => t.Name)
            .ToList();

        // Assert
        Assert.Contains("comment", nonGrammarTerminalNames);
        Assert.Contains("line_comment", nonGrammarTerminalNames);
    }

    [Fact]
    public void Parse_WithBlockComment_Succeeds()
    {
        // Arrange
        TestGrammar grammar = new(registerComments: true);

        // Act
        var node = GrammarParser.Parse(grammar, "/* a comment */ MyId");

        // Assert
        Assert.NotNull(node);
    }

    [Fact]
    public void Parse_WithLineComment_Succeeds()
    {
        // Arrange
        TestGrammar grammar = new(registerComments: true);

        // Act
        var node = GrammarParser.Parse(grammar, "-- a line comment\nMyId");

        // Assert
        Assert.NotNull(node);
    }

    [Fact]
    public void Parse_WithBlockCommentSpanningMultipleLines_Succeeds()
    {
        // Arrange
        TestGrammar grammar = new(registerComments: true);
        var input = "/* line 1\nline 2\nline 3 */ MyId";

        // Act
        var node = GrammarParser.Parse(grammar, input);

        // Assert
        Assert.NotNull(node);
    }

    [Fact]
    public void Parse_WithoutCommentRegistration_BlockCommentProducesErrors()
    {
        // Arrange
        TestGrammar grammar = new(registerComments: false);

        // Act
        var parseTree = GrammarParser.ParseTree(grammar, "/* comment */ MyId");

        // Assert
        Assert.True(parseTree.HasErrors());
    }

    [Fact]
    public void Register_DoesNotAffectGrammarRoot()
    {
        // Arrange
        TestGrammar grammar = new(registerComments: true);

        // Act / Assert: Root remains usable for parsing identifier-only input
        var node = GrammarParser.Parse(grammar, "MyId");
        Assert.NotNull(node);
    }
}
