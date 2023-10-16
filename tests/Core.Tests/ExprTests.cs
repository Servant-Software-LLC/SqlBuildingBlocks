using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class ExprTests
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

            Root = expr;
        }

        public virtual SqlExpression Create(ParseTreeNode expression) => ((Expr)Root).Create(expression);
    }

    [Fact]
    public void ColumnAndIntLiteral_LessThan()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "ID < 3");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.BinExpr);
        var binExpr = expression.BinExpr;

        //Left
        Assert.Null(binExpr.Left.Value);
        Assert.Null(binExpr.Left.BinExpr);
        Assert.NotNull(binExpr.Left.Column);
        Assert.Equal("ID", binExpr.Left.Column.ColumnName);

        //Operator
        Assert.Equal(SqlBinaryOperator.LessThan, binExpr.Operator);

        //Right
        Assert.Null(binExpr.Right.Column);
        Assert.Null(binExpr.Right.BinExpr);
        Assert.NotNull(binExpr.Right.Value);
        Assert.Equal(3, binExpr.Right.Value.Int);
    }

    [Fact]
    public void ColumnAndNullLiteral()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "ID < NULL");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.BinExpr);
        var binExpr = expression.BinExpr;

        //Left
        Assert.Null(binExpr.Left.Value);
        Assert.Null(binExpr.Left.BinExpr);
        Assert.NotNull(binExpr.Left.Column);
        Assert.Equal("ID", binExpr.Left.Column.ColumnName);

        //Operator
        Assert.Equal(SqlBinaryOperator.LessThan, binExpr.Operator);

        //Right (Expecting a NULL literal value, instead of it parsing this as a Column id.
        Assert.Null(binExpr.Right.Column);
        Assert.Null(binExpr.Right.BinExpr);
        Assert.NotNull(binExpr.Right.Value);
        Assert.Null(binExpr.Right.Value.Value);
    }


    [Fact]
    public void ColumnAndParameter_Equal()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "[ID] = @ID");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.BinExpr);
        var binExpr = expression.BinExpr;

        //Left
        Assert.Null(binExpr.Left.Value);
        Assert.Null(binExpr.Left.BinExpr);
        Assert.Null(binExpr.Left.Parameter);
        Assert.NotNull(binExpr.Left.Column);
        Assert.Equal("ID", binExpr.Left.Column.ColumnName);

        //Operator
        Assert.Equal(SqlBinaryOperator.Equal, binExpr.Operator);

        //Right
        Assert.Null(binExpr.Right.Column);
        Assert.Null(binExpr.Right.BinExpr);
        Assert.Null(binExpr.Right.Value);
        Assert.NotNull(binExpr.Right.Parameter);
        Assert.Equal(SqlParameter.ParameterType.Named, binExpr.Right.Parameter.Type);
        Assert.Equal("ID", binExpr.Right.Parameter.Name);
    }

}
