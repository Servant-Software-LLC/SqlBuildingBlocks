using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class FuncCallTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            TableName tableName = new(this, aliasOpt, id);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = funcCall;
        }

        public virtual SqlFunction Create(ParseTreeNode node) => ((FuncCall)Root).Create(node);
    }

    [Fact]
    public void CanParseNoArgFunc()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "LAST_INSERT_ID()");
        var func = grammar.Create(node);

        Assert.Equal("LAST_INSERT_ID", func.FunctionName);
        Assert.Empty(func.Arguments);
    }

    [Fact]
    public void CanParseSingleArgFunc()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "FUNC(arg1)");
        var func = grammar.Create(node);

        Assert.Equal("FUNC", func.FunctionName);
        Assert.Single(func.Arguments);

        var firstColumn = func.Arguments[0].Column;
        Assert.NotNull(firstColumn);
        Assert.Equal("arg1", firstColumn.ColumnName);
    }

    [Fact]
    public void CanParseMultipleArgFunc()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "FUNC(arg1, arg2, arg3)");
        var func = grammar.Create(node);

        Assert.Equal("FUNC", func.FunctionName);
        Assert.Equal(3, func.Arguments.Count);

        var firstColumn = func.Arguments[0].Column;
        Assert.NotNull(firstColumn);
        Assert.Equal("arg1", firstColumn.ColumnName);

        var secondColumn = func.Arguments[1].Column;
        Assert.NotNull(secondColumn);
        Assert.Equal("arg2", secondColumn.ColumnName);

        var thirdColumn = func.Arguments[2].Column;
        Assert.NotNull(thirdColumn);
        Assert.Equal("arg3", thirdColumn.ColumnName);
    }

}
