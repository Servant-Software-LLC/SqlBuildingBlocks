using Xunit;
using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Core.Tests;

public class IdTests
{
    // Private test grammar to generate parse trees
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            var simpleId = new SimpleId(this);
            var id = new Id(this, simpleId);

            Root = id;
        }

        public virtual SqlColumnRef CreateColumnRef(ParseTreeNode columnId) =>
            ((Id)Root).CreateColumnRef(columnId);
    }

    [Fact]
    public void CreateColumnRef_WithSinglePartName_ReturnsSqlColumnRef()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "ColumnName");

        var columnRef = grammar.CreateColumnRef(node);

        Assert.Null(columnRef.DatabaseName);
        Assert.Null(columnRef.TableName);
        Assert.Equal("ColumnName", columnRef.ColumnName);
    }

    [Fact]
    public void CreateColumnRef_WithTwoPartName_ReturnsSqlColumnRef()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "TableName.ColumnName");

        var columnRef = grammar.CreateColumnRef(node);

        Assert.Null(columnRef.DatabaseName);
        Assert.Equal("TableName", columnRef.TableName);
        Assert.Equal("ColumnName", columnRef.ColumnName);
    }

    [Fact]
    public void CreateColumnRef_WithThreePartName_ReturnsSqlColumnRef()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "DatabaseName.TableName.ColumnName");

        var columnRef = grammar.CreateColumnRef(node);

        Assert.Equal("DatabaseName", columnRef.DatabaseName);
        Assert.Equal("TableName", columnRef.TableName);
        Assert.Equal("ColumnName", columnRef.ColumnName);
    }

    [Fact]
    public void CreateColumnRef_WithInvalidParts_ThrowsException()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "Part1.Part2.Part3.Part4");

        // Expect an exception when trying to create a SqlColumnRef from a ParseTreeNode with an invalid number of parts
        Assert.Throws<Exception>(() => grammar.CreateColumnRef(node));
    }

}
