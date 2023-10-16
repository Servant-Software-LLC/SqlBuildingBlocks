using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class JoinChainOptTests
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
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = joinChainOpt;
        }

        public virtual IList<SqlJoin> Create(ParseTreeNode joinChainOptNode) => ((JoinChainOpt)Root).Create(joinChainOptNode);
    }

    [Fact]
    public void MultipleInnerJoins_WithColumnId_Expressions()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"INNER JOIN [Orders] [o] ON [c].[ID] = [o].[CustomerID] 
                                                      LEFT JOIN [OrderItems] [oi] ON [o].[ID] = [oi].[OrderID] 
                                                      RIGHT JOIN [Products] [p] ON [p].[ID] = [oi].[ProductID]");
        var joinChainOpt = grammar.Create(node);

        Assert.Equal(3, joinChainOpt.Count);

        var firstJoin = joinChainOpt[0];
        Assert.Equal(SqlJoinKind.Inner, firstJoin.JoinKind);
        Assert.Equal("Orders", firstJoin.Table.TableName);
        Assert.Equal("o", firstJoin.Table.TableAlias);
        Assert.NotNull(firstJoin.Condition.Left.Column);
        Assert.Equal("ID", firstJoin.Condition.Left.Column.ColumnName);
        Assert.Equal("c", firstJoin.Condition.Left.Column.TableName);
        Assert.NotNull(firstJoin.Condition.Right.Column);
        Assert.Equal("CustomerID", firstJoin.Condition.Right.Column.ColumnName);
        Assert.Equal("o", firstJoin.Condition.Right.Column.TableName);

        var secondJoin = joinChainOpt[1];
        Assert.Equal(SqlJoinKind.Left, secondJoin.JoinKind);
        Assert.Equal("OrderItems", secondJoin.Table.TableName);
        Assert.Equal("oi", secondJoin.Table.TableAlias);
        Assert.NotNull(secondJoin.Condition.Left.Column);
        Assert.Equal("ID", secondJoin.Condition.Left.Column.ColumnName);
        Assert.Equal("o", secondJoin.Condition.Left.Column.TableName);
        Assert.NotNull(secondJoin.Condition.Right.Column);
        Assert.Equal("OrderID", secondJoin.Condition.Right.Column.ColumnName);
        Assert.Equal("oi", secondJoin.Condition.Right.Column.TableName);

        var thirdJoin = joinChainOpt[2];
        Assert.Equal(SqlJoinKind.Right, thirdJoin.JoinKind);
        Assert.Equal("Products", thirdJoin.Table.TableName);
        Assert.Equal("p", thirdJoin.Table.TableAlias);
        Assert.NotNull(thirdJoin.Condition.Left.Column);
        Assert.Equal("ID", thirdJoin.Condition.Left.Column.ColumnName);
        Assert.Equal("p", thirdJoin.Condition.Left.Column.TableName);
        Assert.NotNull(thirdJoin.Condition.Right.Column);
        Assert.Equal("ProductID", thirdJoin.Condition.Right.Column.ColumnName);
        Assert.Equal("oi", thirdJoin.Condition.Right.Column.TableName);


    }
}
