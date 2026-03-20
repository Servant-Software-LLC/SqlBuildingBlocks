using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.PostgreSQL.Tests;

public class ArrayTests
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
            PostgreSQL.Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            PostgreSQL.DataType dataType = new(this);
            expr.InitializeRule(selectStmt, funcCall, dataType);
            expr.AddPgCastSupport(this, dataType);
            expr.AddArrayConstructorSupport(this);
            expr.AddArraySubscriptSupport(this);

            Root = expr;
        }

        public SqlExpression Create(ParseTreeNode expression) =>
            ((PostgreSQL.Expr)Root).Create(expression);
    }

    // ── ARRAY constructor ───────────────────────────────────────────────────

    [Fact]
    public void ArrayConstructor_IntegerLiterals()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "ARRAY[1, 2, 3]");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.ArrayConstructor);
        var arr = expression.ArrayConstructor!;
        Assert.Equal(3, arr.Items.Count);
        Assert.NotNull(arr.Items[0].Value);
        Assert.NotNull(arr.Items[1].Value);
        Assert.NotNull(arr.Items[2].Value);
    }

    [Fact]
    public void ArrayConstructor_StringLiterals()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "ARRAY['a', 'b', 'c']");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.ArrayConstructor);
        var arr = expression.ArrayConstructor!;
        Assert.Equal(3, arr.Items.Count);
        Assert.Equal("a", arr.Items[0].Value!.String);
        Assert.Equal("b", arr.Items[1].Value!.String);
        Assert.Equal("c", arr.Items[2].Value!.String);
    }

    [Fact]
    public void ArrayConstructor_ColumnRefs()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "ARRAY[col1, col2]");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.ArrayConstructor);
        var arr = expression.ArrayConstructor!;
        Assert.Equal(2, arr.Items.Count);
        Assert.Equal("col1", arr.Items[0].Column!.ColumnName);
        Assert.Equal("col2", arr.Items[1].Column!.ColumnName);
    }

    [Fact]
    public void ArrayConstructor_SingleElement()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "ARRAY[42]");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.ArrayConstructor);
        Assert.Single(expression.ArrayConstructor!.Items);
    }

    [Fact]
    public void ArrayConstructor_CaseInsensitive()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "array[1, 2]");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.ArrayConstructor);
        Assert.Equal(2, expression.ArrayConstructor!.Items.Count);
    }

    [Fact]
    public void ArrayConstructor_ToExpressionString()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "ARRAY[1, 2, 3]");
        var expression = grammar.Create(node);

        Assert.Equal("ARRAY[1, 2, 3]", expression.ToExpressionString());
    }

    // ── Array literal with :: cast ──────────────────────────────────────────

    [Fact]
    public void ArrayLiteral_CastToIntegerArray()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "'{1,2,3}'::INTEGER[]");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        var cast = expression.CastExpr!;
        Assert.NotNull(cast.Expression.Value);
        Assert.Equal("{1,2,3}", cast.Expression.Value!.String);
        Assert.Equal("INTEGER", cast.DataType.Name);
        Assert.Equal(1, cast.DataType.ArrayDimensions);
    }

    [Fact]
    public void ArrayLiteral_CastToTextArray()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "'{hello,world}'::TEXT[]");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        Assert.Equal("TEXT", expression.CastExpr!.DataType.Name);
        Assert.Equal(1, expression.CastExpr.DataType.ArrayDimensions);
    }

    [Fact]
    public void ArrayLiteral_CastToMultiDimensionalArray()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "'{{1,2},{3,4}}'::INTEGER[][]");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        Assert.Equal("INTEGER", expression.CastExpr!.DataType.Name);
        Assert.Equal(2, expression.CastExpr.DataType.ArrayDimensions);
    }

    // ── ANY(ARRAY[...]) and ALL(ARRAY[...]) ─────────────────────────────────

    [Fact]
    public void AnyArray_ParsesAsFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "col = ANY(ARRAY[1, 2, 3])");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.BinExpr);
        var binExpr = expression.BinExpr!;
        Assert.Equal(SqlBinaryOperator.Equal, binExpr.Operator);

        Assert.NotNull(binExpr.Left.Column);
        Assert.Equal("col", binExpr.Left.Column!.ColumnName);

        Assert.NotNull(binExpr.Right.Function);
        Assert.Equal("ANY", binExpr.Right.Function!.FunctionName);
        Assert.Single(binExpr.Right.Function.Arguments);
        Assert.NotNull(binExpr.Right.Function.Arguments[0].ArrayConstructor);
    }

    [Fact]
    public void AllArray_ParsesAsFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "col > ALL(ARRAY[10, 20])");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.BinExpr);
        var binExpr = expression.BinExpr!;

        Assert.NotNull(binExpr.Right.Function);
        Assert.Equal("ALL", binExpr.Right.Function!.FunctionName);
        Assert.Single(binExpr.Right.Function.Arguments);
        Assert.NotNull(binExpr.Right.Function.Arguments[0].ArrayConstructor);
    }

    // ── Array subscript ─────────────────────────────────────────────────────

    [Fact]
    public void ArraySubscript_SingleIndex()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "col[1]");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.ArraySubscript);
        var sub = expression.ArraySubscript!;
        Assert.False(sub.IsSlice);
        Assert.NotNull(sub.Array.Column);
        Assert.Equal("col", sub.Array.Column!.ColumnName);
        Assert.NotNull(sub.Index!.Value);
    }

    [Fact]
    public void ArraySubscript_Slice()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "col[1:3]");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.ArraySubscript);
        var sub = expression.ArraySubscript!;
        Assert.True(sub.IsSlice);
        Assert.NotNull(sub.Array.Column);
        Assert.Equal("col", sub.Array.Column!.ColumnName);
        Assert.NotNull(sub.LowerBound);
        Assert.NotNull(sub.UpperBound);
    }

    [Fact]
    public void ArraySubscript_ToExpressionString_Index()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "col[2]");
        var expression = grammar.Create(node);

        Assert.Equal("col[2]", expression.ToExpressionString());
    }

    [Fact]
    public void ArraySubscript_ToExpressionString_Slice()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "col[1:3]");
        var expression = grammar.Create(node);

        Assert.Equal("col[1:3]", expression.ToExpressionString());
    }

    // ── Array column types ──────────────────────────────────────────────────

    private class DataTypeTestGrammar : Grammar
    {
        public DataTypeTestGrammar() : base(false)
        {
            PostgreSQL.DataType dataType = new(this);
            Root = dataType;
        }

        public SqlDataType Create(ParseTreeNode node) =>
            ((PostgreSQL.DataType)Root).Create(node);
    }

    [Fact]
    public void ArrayColumnType_IntegerArray()
    {
        DataTypeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "INTEGER[]");
        var sqlDataType = grammar.Create(node);

        Assert.Equal("INTEGER", sqlDataType.Name);
        Assert.Equal(1, sqlDataType.ArrayDimensions);
    }

    [Fact]
    public void ArrayColumnType_TextMultiDimensional()
    {
        DataTypeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "TEXT[][]");
        var sqlDataType = grammar.Create(node);

        Assert.Equal("TEXT", sqlDataType.Name);
        Assert.Equal(2, sqlDataType.ArrayDimensions);
    }

    [Fact]
    public void ArrayColumnType_VarcharArray()
    {
        DataTypeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "VARCHAR(100)[]");
        var sqlDataType = grammar.Create(node);

        Assert.Equal("VARCHAR", sqlDataType.Name);
        Assert.Equal(100, sqlDataType.Length);
        Assert.Equal(1, sqlDataType.ArrayDimensions);
    }

    [Fact]
    public void ArrayColumnType_NumericArrayWithPrecision()
    {
        DataTypeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "NUMERIC(10,2)[]");
        var sqlDataType = grammar.Create(node);

        Assert.Equal("NUMERIC", sqlDataType.Name);
        Assert.Equal(10, sqlDataType.Precision);
        Assert.Equal(2, sqlDataType.Scale);
        Assert.Equal(1, sqlDataType.ArrayDimensions);
    }

    [Fact]
    public void ArrayColumnType_NonArray()
    {
        DataTypeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "INTEGER");
        var sqlDataType = grammar.Create(node);

        Assert.Equal("INTEGER", sqlDataType.Name);
        Assert.Equal(0, sqlDataType.ArrayDimensions);
    }

    [Fact]
    public void ArrayColumnType_CaseInsensitive()
    {
        DataTypeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "integer[]");
        var sqlDataType = grammar.Create(node);

        Assert.Equal("INTEGER", sqlDataType.Name);
        Assert.Equal(1, sqlDataType.ArrayDimensions);
    }
}
