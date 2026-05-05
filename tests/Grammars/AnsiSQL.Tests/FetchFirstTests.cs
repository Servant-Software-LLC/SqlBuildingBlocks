using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.AnsiSQL.Tests;

/// <summary>
/// Tests for the ANSI-specific <c>FETCH FIRST n ROWS ONLY</c> syntax.
/// The AnsiSQL <see cref="SelectStmt"/> appends a <see cref="FetchFirstOpt"/> non-terminal
/// to the rule. The base <c>SelectStmt.Update</c> does not consume this child, so these
/// tests primarily verify that the grammar accepts the syntax without parse errors and
/// that the FETCH FIRST clause does not break SELECT AST construction.
/// </summary>
public class FetchFirstTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar() : base(false) // SQL is case insensitive
        {
            SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            AnsiSQL.SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = selectStmt;
        }

        public virtual SqlSelectDefinition Create(ParseTreeNode selectStmt) =>
            ((SelectStmt)Root).Create(selectStmt);

        public virtual SqlSelectDefinition Create(ParseTreeNode selectStmt,
            IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider) =>
            ((SelectStmt)Root).Create(selectStmt, databaseConnectionProvider, tableSchemaProvider);
    }

    [Fact]
    public void FetchFirst_BareNumberOnly_Parses()
    {
        TestGrammar grammar = new();
        var parseTree = GrammarParser.ParseTree(grammar, "SELECT * FROM Customers FETCH FIRST 10 ROWS ONLY");

        Assert.False(parseTree.HasErrors());
    }

    [Fact]
    public void FetchFirst_WithOrderBy_Parses()
    {
        TestGrammar grammar = new();
        var parseTree = GrammarParser.ParseTree(grammar,
            "SELECT * FROM Customers ORDER BY CustomerName FETCH FIRST 5 ROWS ONLY");

        Assert.False(parseTree.HasErrors());
    }

    [Fact]
    public void FetchFirst_WithLargerNumber_Parses()
    {
        TestGrammar grammar = new();
        var parseTree = GrammarParser.ParseTree(grammar,
            "SELECT ID, CustomerName FROM Customers FETCH FIRST 100 ROWS ONLY");

        Assert.False(parseTree.HasErrors());
    }

    [Fact]
    public void FetchFirst_StillBuildsSelectAst_BasicShape()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers FETCH FIRST 10 ROWS ONLY");

        // The AnsiSQL FetchFirstOpt does not currently populate a logical entity, but
        // the SELECT AST itself must still be produced cleanly.
        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        Assert.IsType<SqlAllColumns>(selectStmt.Columns[0]);
        Assert.NotNull(selectStmt.Table);
        Assert.Equal("Customers", selectStmt.Table.TableName);
    }

    [Fact]
    public void Select_WithoutFetchFirst_StillParses()
    {
        // Sanity check: omitting FETCH FIRST (the empty alternative of FetchFirstOpt) also works.
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM Customers");

        var selectStmt = grammar.Create(node);
        Assert.Single(selectStmt.Columns);
    }
}
