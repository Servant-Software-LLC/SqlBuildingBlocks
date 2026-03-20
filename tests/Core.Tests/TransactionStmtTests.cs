using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class TransactionStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            TransactionStmt transactionStmt = new(this);

            Root = transactionStmt;
        }

        public virtual SqlTransactionDefinition Create(ParseTreeNode node) =>
            ((TransactionStmt)Root).Create(node);
    }

    [Fact]
    public void Begin_Simple()
    {
        //Setup
        const string sql = "BEGIN";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlTransactionKind.Begin, result.Kind);
        Assert.Null(result.IsolationLevel);
    }

    [Fact]
    public void Begin_Transaction()
    {
        //Setup
        const string sql = "BEGIN TRANSACTION";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlTransactionKind.Begin, result.Kind);
        Assert.Null(result.IsolationLevel);
    }

    [Fact]
    public void Start_Transaction()
    {
        //Setup
        const string sql = "START TRANSACTION";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlTransactionKind.Begin, result.Kind);
        Assert.Null(result.IsolationLevel);
    }

    [Fact]
    public void Begin_Transaction_IsolationLevel_ReadCommitted()
    {
        //Setup
        const string sql = "BEGIN TRANSACTION ISOLATION LEVEL READ COMMITTED";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlTransactionKind.Begin, result.Kind);
        Assert.Equal("READ COMMITTED", result.IsolationLevel);
    }

    [Fact]
    public void Begin_Transaction_IsolationLevel_Serializable()
    {
        //Setup
        const string sql = "BEGIN TRANSACTION ISOLATION LEVEL SERIALIZABLE";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlTransactionKind.Begin, result.Kind);
        Assert.Equal("SERIALIZABLE", result.IsolationLevel);
    }

    [Fact]
    public void Begin_Transaction_IsolationLevel_RepeatableRead()
    {
        //Setup
        const string sql = "BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlTransactionKind.Begin, result.Kind);
        Assert.Equal("REPEATABLE READ", result.IsolationLevel);
    }

    [Fact]
    public void Begin_Transaction_IsolationLevel_ReadUncommitted()
    {
        //Setup
        const string sql = "BEGIN TRANSACTION ISOLATION LEVEL READ UNCOMMITTED";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlTransactionKind.Begin, result.Kind);
        Assert.Equal("READ UNCOMMITTED", result.IsolationLevel);
    }

    [Fact]
    public void Commit_Simple()
    {
        //Setup
        const string sql = "COMMIT";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlTransactionKind.Commit, result.Kind);
        Assert.Null(result.IsolationLevel);
    }

    [Fact]
    public void Commit_Transaction()
    {
        //Setup
        const string sql = "COMMIT TRANSACTION";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlTransactionKind.Commit, result.Kind);
    }

    [Fact]
    public void Rollback_Simple()
    {
        //Setup
        const string sql = "ROLLBACK";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlTransactionKind.Rollback, result.Kind);
        Assert.Null(result.IsolationLevel);
    }

    [Fact]
    public void Rollback_Transaction()
    {
        //Setup
        const string sql = "ROLLBACK TRANSACTION";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal(SqlTransactionKind.Rollback, result.Kind);
    }
}
