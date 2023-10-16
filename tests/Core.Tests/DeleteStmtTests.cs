using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class DeleteStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
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
            ReturningClauseOpt returningClauseOpt = new(this, id);
            DeleteStmt deleteStmt = new(this, tableName, whereClauseOpt, returningClauseOpt);

            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = deleteStmt;
        }

        public virtual SqlDeleteDefinition Create(ParseTreeNode deleteStmt) => ((DeleteStmt)Root).Create(deleteStmt);
    }

    [Fact]
    public void DeleteStmt_WhereClause()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "DELETE FROM employees WHERE name='Joe'");
        var deleteStmt = grammar.Create(node);

        //Assert Table
        Assert.NotNull(deleteStmt.Table);
        Assert.Equal("employees", deleteStmt.Table.TableName);

        //Assert WHERE
        var whereClause = deleteStmt.WhereClause;
        Assert.NotNull(whereClause);
        Assert.NotNull(whereClause.Left.Column);
        Assert.Equal("name", whereClause.Left.Column.ColumnName);
        Assert.NotNull(whereClause.Right.Value);
        Assert.Equal("Joe", whereClause.Right.Value.String);
    }

    [Fact]
    public void DeleteStmt_ReturnInteger()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "DELETE FROM employees RETURNING 1");
        var deleteStmt = grammar.Create(node);

        //Assert Table
        Assert.NotNull(deleteStmt.Table);
        Assert.Equal("employees", deleteStmt.Table.TableName);

        //Assert WHERE
        var whereClause = deleteStmt.WhereClause;
        Assert.Null(whereClause);

        //Assert Returning
        var returning = deleteStmt.Returning;
        Assert.NotNull(returning);
        Assert.Equal(1, returning.Int);
        Assert.Null(returning.Column);
    }

}
