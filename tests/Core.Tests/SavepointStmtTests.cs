using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class SavepointStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            Id id = new(this, simpleId);

            SavepointStmt savepointStmt = new(this, id);

            Root = savepointStmt;
        }

        public virtual SqlSavepointDefinition Create(ParseTreeNode node) =>
            ((SavepointStmt)Root).Create(node);
    }

    [Fact]
    public void Savepoint_Create()
    {
        //Setup
        const string sql = "SAVEPOINT my_save";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlSavepointKind.Create, result.Kind);
        Assert.Equal("my_save", result.Name);
    }

    [Fact]
    public void Savepoint_Release()
    {
        //Setup
        const string sql = "RELEASE SAVEPOINT my_save";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlSavepointKind.Release, result.Kind);
        Assert.Equal("my_save", result.Name);
    }

    [Fact]
    public void Savepoint_Release_WithoutKeyword()
    {
        //Setup
        const string sql = "RELEASE my_save";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlSavepointKind.Release, result.Kind);
        Assert.Equal("my_save", result.Name);
    }

    [Fact]
    public void Savepoint_RollbackTo()
    {
        //Setup
        const string sql = "ROLLBACK TO SAVEPOINT my_save";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlSavepointKind.Rollback, result.Kind);
        Assert.Equal("my_save", result.Name);
    }

    [Fact]
    public void Savepoint_RollbackTo_WithoutKeyword()
    {
        //Setup
        const string sql = "ROLLBACK TO my_save";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlSavepointKind.Rollback, result.Kind);
        Assert.Equal("my_save", result.Name);
    }
}
