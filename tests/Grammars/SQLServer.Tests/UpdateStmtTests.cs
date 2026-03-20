using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.SQLServer.Tests;

public class UpdateStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SQLServer.SimpleId simpleId = new(this);

            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableHintOpt tableHintOpt = new(this);
            TableName tableName = new(this, aliasOpt, id, tableHintOpt);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            ReturningClauseOpt returningClauseOpt = new(this, id);
            UpdateStmt updateStmt = new(this, id, literalValue, parameter, funcCall, tableName, whereClauseOpt, returningClauseOpt, joinChainOpt);

            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = updateStmt;
        }

        public SqlUpdateDefinition Create(ParseTreeNode updateStmt) => ((UpdateStmt)Root).Create(updateStmt);
    }

    [Fact]
    public void Update_WithFromAndJoin()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE c SET [c].[Status] = 'Active' FROM [Customers] c INNER JOIN [Orders] o ON [c].[ID] = [o].[CustomerID] WHERE [o].[Shipped] = 0");

        var updateStmt = grammar.Create(node);

        Assert.NotNull(updateStmt.Table);
        Assert.Equal("c", updateStmt.Table!.TableName);

        Assert.NotNull(updateStmt.SourceTable);
        Assert.Equal("Customers", updateStmt.SourceTable!.TableName);
        Assert.Equal("c", updateStmt.SourceTable.TableAlias);

        Assert.Single(updateStmt.Joins);

        var join = updateStmt.Joins[0];
        Assert.Equal(SqlJoinKind.Inner, join.JoinKind);
        Assert.Equal("Orders", join.Table.TableName);
        Assert.Equal("o", join.Table.TableAlias);
        Assert.Equal("c", join.Condition.Left.Column!.TableName);
        Assert.Equal("ID", join.Condition.Left.Column.ColumnName);
        Assert.Equal("o", join.Condition.Right!.Column!.TableName);
        Assert.Equal("CustomerID", join.Condition.Right.Column.ColumnName);
    }

    [Fact]
    public void Update_BracketQuoted_TableAndColumns()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "UPDATE [Customers] SET [CustomerName] = 'Bob' WHERE [ID] = 1");

        var updateStmt = grammar.Create(node);

        Assert.NotNull(updateStmt.Table);
        Assert.Equal("Customers", updateStmt.Table!.TableName);
        Assert.NotNull(updateStmt.WhereClause);
    }

    [Fact]
    public void Update_BracketQuoted_WithJoin()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "UPDATE [Customers] [c] INNER JOIN [Orders] [o] ON [c].[ID] = [o].[CustomerID] SET [c].[CustomerName] = 'Active' WHERE [o].[OrderDate] = '2024-01-01'");

        var updateStmt = grammar.Create(node);

        Assert.NotNull(updateStmt.Table);
        Assert.Equal("Customers", updateStmt.Table!.TableName);
        Assert.Equal("c", updateStmt.Table.TableAlias);
        Assert.Single(updateStmt.Joins);
        Assert.Equal("Orders", updateStmt.Joins[0].Table.TableName);
    }
}
