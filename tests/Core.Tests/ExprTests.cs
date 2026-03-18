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

    [Fact]
    public void Column_IsNull()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "manager_id IS NULL");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.BinExpr);
        var binExpr = expression.BinExpr;

        // Left: the column operand
        Assert.NotNull(binExpr.Left.Column);
        Assert.Equal("manager_id", binExpr.Left.Column.ColumnName);
        Assert.Null(binExpr.Left.Value);
        Assert.Null(binExpr.Left.BinExpr);

        // Operator
        Assert.Equal(SqlBinaryOperator.IsNull, binExpr.Operator);

        // Right: null (IS NULL is a unary postfix predicate)
        Assert.Null(binExpr.Right);
    }

    [Fact]
    public void Column_IsNotNull()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "shipped_date IS NOT NULL");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.BinExpr);
        var binExpr = expression.BinExpr;

        // Left: the column operand
        Assert.NotNull(binExpr.Left.Column);
        Assert.Equal("shipped_date", binExpr.Left.Column.ColumnName);
        Assert.Null(binExpr.Left.Value);
        Assert.Null(binExpr.Left.BinExpr);

        // Operator
        Assert.Equal(SqlBinaryOperator.IsNotNull, binExpr.Operator);

        // Right: null (IS NOT NULL is a unary postfix predicate)
        Assert.Null(binExpr.Right);
    }

    [Fact]
    public void CompoundCondition_IsNull_And_IsNotNull()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "a IS NULL AND b IS NOT NULL");
        var expression = grammar.Create(node);

        // Top-level: AND binary expression
        Assert.NotNull(expression.BinExpr);
        var andExpr = expression.BinExpr;
        Assert.Equal(SqlBinaryOperator.And, andExpr.Operator);

        // Left of AND: a IS NULL
        Assert.NotNull(andExpr.Left.BinExpr);
        var leftIsNull = andExpr.Left.BinExpr;
        Assert.Equal(SqlBinaryOperator.IsNull, leftIsNull.Operator);
        Assert.NotNull(leftIsNull.Left.Column);
        Assert.Equal("a", leftIsNull.Left.Column.ColumnName);
        Assert.Null(leftIsNull.Right);

        // Right of AND: b IS NOT NULL
        Assert.NotNull(andExpr.Right);
        Assert.NotNull(andExpr.Right.BinExpr);
        var rightIsNotNull = andExpr.Right.BinExpr;
        Assert.Equal(SqlBinaryOperator.IsNotNull, rightIsNotNull.Operator);
        Assert.NotNull(rightIsNotNull.Left.Column);
        Assert.Equal("b", rightIsNotNull.Left.Column.ColumnName);
        Assert.Null(rightIsNotNull.Right);
    }

    [Fact]
    public void IsNull_ToExpressionString()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "manager_id IS NULL");
        var expression = grammar.Create(node);

        Assert.Equal("manager_id IS NULL", expression.ToExpressionString());
    }

    [Fact]
    public void IsNotNull_ToExpressionString()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "shipped_date IS NOT NULL");
        var expression = grammar.Create(node);

        Assert.Equal("shipped_date IS NOT NULL", expression.ToExpressionString());
    }

    // ── BETWEEN / NOT BETWEEN ─────────────────────────────────────────────

    [Fact]
    public void Between_IntegerLiterals()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "amount BETWEEN 100 AND 500");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.BetweenExpr);
        var between = expression.BetweenExpr!;

        Assert.False(between.IsNegated);

        // Operand
        Assert.NotNull(between.Operand.Column);
        Assert.Equal("amount", between.Operand.Column.ColumnName);

        // Lower bound
        Assert.NotNull(between.LowerBound.Value);
        Assert.Equal(100, between.LowerBound.Value.Int);

        // Upper bound
        Assert.NotNull(between.UpperBound.Value);
        Assert.Equal(500, between.UpperBound.Value.Int);
    }

    [Fact]
    public void NotBetween_IntegerLiterals()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "age NOT BETWEEN 18 AND 25");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.BetweenExpr);
        var between = expression.BetweenExpr!;

        Assert.True(between.IsNegated);

        // Operand
        Assert.NotNull(between.Operand.Column);
        Assert.Equal("age", between.Operand.Column.ColumnName);

        // Lower bound
        Assert.NotNull(between.LowerBound.Value);
        Assert.Equal(18, between.LowerBound.Value.Int);

        // Upper bound
        Assert.NotNull(between.UpperBound.Value);
        Assert.Equal(25, between.UpperBound.Value.Int);
    }

    [Fact]
    public void Between_ColumnReferenceBounds()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "created_at BETWEEN start_date AND end_date");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.BetweenExpr);
        var between = expression.BetweenExpr!;

        Assert.False(between.IsNegated);

        Assert.NotNull(between.Operand.Column);
        Assert.Equal("created_at", between.Operand.Column.ColumnName);

        Assert.NotNull(between.LowerBound.Column);
        Assert.Equal("start_date", between.LowerBound.Column.ColumnName);

        Assert.NotNull(between.UpperBound.Column);
        Assert.Equal("end_date", between.UpperBound.Column.ColumnName);
    }

    [Fact]
    public void Between_ToExpressionString()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "amount BETWEEN 100 AND 500");
        var expression = grammar.Create(node);

        Assert.Equal("amount BETWEEN 100 AND 500", expression.ToExpressionString());
    }

    [Fact]
    public void NotBetween_ToExpressionString()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "age NOT BETWEEN 18 AND 25");
        var expression = grammar.Create(node);

        Assert.Equal("age NOT BETWEEN 18 AND 25", expression.ToExpressionString());
    }

}
