using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.AnsiSQL.Tests;

public class DeleteStmtTests
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
            ReturningClauseOpt returningClauseOpt = new(this, id);
            AnsiSQL.SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            DeleteStmt deleteStmt = new(this, tableName, whereClauseOpt, returningClauseOpt, joinChainOpt);

            Root = deleteStmt;
        }

        public virtual SqlDeleteDefinition Create(ParseTreeNode deleteStmt) =>
            ((DeleteStmt)Root).Create(deleteStmt);
    }

    [Fact]
    public void Delete_WithWhere()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "DELETE FROM employees WHERE name = 'Joe'");

        var deleteStmt = grammar.Create(node);

        Assert.NotNull(deleteStmt.Table);
        Assert.Equal("employees", deleteStmt.Table!.TableName);

        Assert.NotNull(deleteStmt.WhereClause);
        Assert.Equal("name", deleteStmt.WhereClause!.BinExpr!.Left.Column!.ColumnName);
        Assert.Equal("Joe", deleteStmt.WhereClause.BinExpr.Right!.Value!.String);
    }

    [Fact]
    public void Delete_WithoutWhere()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "DELETE FROM events");

        var deleteStmt = grammar.Create(node);

        Assert.Equal("events", deleteStmt.Table!.TableName);
        Assert.Null(deleteStmt.WhereClause);
    }

    [Fact]
    public void Delete_WithComplexWhere()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "DELETE FROM logs WHERE level = 'DEBUG' AND archived = 1");

        var deleteStmt = grammar.Create(node);

        Assert.Equal("logs", deleteStmt.Table!.TableName);
        Assert.NotNull(deleteStmt.WhereClause);
        // The top-level AND binds two equality expressions
        Assert.NotNull(deleteStmt.WhereClause!.BinExpr);
    }
}
