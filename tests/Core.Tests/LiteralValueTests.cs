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
    public void CanCreateDecimalLiteralValue_FromUnsuffixedFractionalLiteral()
    {
        // Issue #184: an unsuffixed fractional literal like 3.00 must be representable in
        // SqlLiteralValue. The grammar's NumberLiteral is configured (DefaultFloatType =
        // TypeCode.Decimal) so unsuffixed decimal literals materialize as System.Decimal,
        // preserving precision and matching the CLR type used for typed decimal columns.
        var input = "3.00";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, input);
        var literalValue = grammar.Create(node);

        Assert.Equal(3.00m, literalValue.Decimal);
        Assert.Null(literalValue.Double);
        Assert.Null(literalValue.Int);
        Assert.IsType<decimal>(literalValue.Value);
    }

    [Fact]
    public void CanCreateDecimalLiteralValue_PreservesScale()
    {
        // The grammar must preserve the literal's scale — `3.00` is not the same numeric
        // value as `3` for purposes of round-tripping or formatting; it remains decimal(3,2).
        var input = "12.345";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, input);
        var literalValue = grammar.Create(node);

        Assert.Equal(12.345m, literalValue.Decimal);
        Assert.Equal("12.345", literalValue.ToString());
    }

    [Fact]
    public void CanCreateDoubleLiteralValue_FromScientificNotation()
    {
        // Scientific notation literals (e.g., 3.0e2) still materialize as double because Irony's
        // NumberLiteral handles the exponent path separately from DefaultFloatType. The Create
        // method must accept that double too — pre-#184 it would have thrown.
        var input = "3.0e2";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, input);
        var literalValue = grammar.Create(node);

        // 3.0e2 == 300 — the runtime type may be either double (Irony's exponent handling)
        // or decimal depending on the grammar's NumberLiteral configuration. Both are
        // acceptable runtime types; the important point is that Create() does not throw and
        // produces a numeric value equivalent to 300.
        Assert.True(literalValue.Double != null || literalValue.Decimal != null);
        Assert.Equal(300m, Convert.ToDecimal(literalValue.Value));
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
