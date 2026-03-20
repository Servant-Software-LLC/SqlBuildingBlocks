using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.MySQL.Tests;

public class DeleteStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            MySQL.SimpleId simpleId = new(this);

            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            ReturningClauseOpt returningClauseOpt = new(this, id);
            DeleteStmt deleteStmt = new(this, tableName, whereClauseOpt, returningClauseOpt, joinChainOpt);

            FuncCall funcCall = new(this, id, expr);
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = deleteStmt;
        }

        public SqlDeleteDefinition Create(ParseTreeNode deleteStmt) => ((DeleteStmt)Root).Create(deleteStmt);
    }

    [Fact]
    public void Delete_BacktickQuoted_TableName()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "DELETE FROM `Customers` WHERE `ID` = 1");

        var deleteStmt = grammar.Create(node);

        Assert.NotNull(deleteStmt.Table);
        Assert.Equal("Customers", deleteStmt.Table!.TableName);
        Assert.NotNull(deleteStmt.WhereClause);
    }

    [Fact]
    public void Delete_BacktickQuoted_WithJoin()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "DELETE `o` FROM `orders` `o` JOIN `Customers` `c` ON `o`.`customer_id` = `c`.`ID` WHERE `c`.`CustomerName` = 'test'");

        var deleteStmt = grammar.Create(node);

        Assert.NotNull(deleteStmt.Table);
        Assert.Equal("o", deleteStmt.Table!.TableName);
        Assert.NotNull(deleteStmt.SourceTable);
        Assert.Equal("orders", deleteStmt.SourceTable!.TableName);
        Assert.Single(deleteStmt.Joins);
        Assert.Equal("Customers", deleteStmt.Joins[0].Table.TableName);
    }

    [Fact]
    public void Delete_WithJoin()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "DELETE o FROM orders o JOIN customers c ON o.customer_id = c.id WHERE c.deleted = 1");

        var deleteStmt = grammar.Create(node);

        Assert.NotNull(deleteStmt.Table);
        Assert.Equal("o", deleteStmt.Table!.TableName);

        Assert.NotNull(deleteStmt.SourceTable);
        Assert.Equal("orders", deleteStmt.SourceTable!.TableName);
        Assert.Equal("o", deleteStmt.SourceTable.TableAlias);

        Assert.Single(deleteStmt.Joins);

        var join = deleteStmt.Joins[0];
        Assert.Equal(SqlJoinKind.Inner, join.JoinKind);
        Assert.Equal("customers", join.Table.TableName);
        Assert.Equal("c", join.Table.TableAlias);
        Assert.Equal("o", join.Condition.Left.Column!.TableName);
        Assert.Equal("customer_id", join.Condition.Left.Column.ColumnName);
        Assert.Equal("c", join.Condition.Right!.Column!.TableName);
        Assert.Equal("id", join.Condition.Right.Column.ColumnName);
    }
}
