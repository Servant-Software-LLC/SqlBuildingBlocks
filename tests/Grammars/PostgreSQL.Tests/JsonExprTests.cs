using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.PostgreSQL.Tests;

public class JsonExprTests
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
            expr.AddJsonOperatorSupport(this);

            Root = expr;
        }

        public SqlExpression Create(ParseTreeNode expression) =>
            ((PostgreSQL.Expr)Root).Create(expression);
    }

    // ── -> operator (JSON object field, returns JSON) ────────────────────

    [Fact]
    public void JsonArrow_ColumnAndStringKey()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data -> 'key'");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.JsonExpr);
        var json = expression.JsonExpr!;

        Assert.Equal(SqlJsonOperator.Arrow, json.Operator);
        Assert.NotNull(json.Left.Column);
        Assert.Equal("data", json.Left.Column!.ColumnName);
        Assert.NotNull(json.Right.Value);
        Assert.Equal("key", json.Right.Value!.String);
    }

    [Fact]
    public void JsonArrow_ColumnAndIntegerIndex()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data -> 0");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.JsonExpr);
        Assert.Equal(SqlJsonOperator.Arrow, expression.JsonExpr!.Operator);
        Assert.NotNull(expression.JsonExpr.Left.Column);
        Assert.Equal("data", expression.JsonExpr.Left.Column!.ColumnName);
    }

    // ── ->> operator (JSON object field, returns text) ───────────────────

    [Fact]
    public void JsonDoubleArrow_ColumnAndStringKey()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data ->> 'name'");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.JsonExpr);
        var json = expression.JsonExpr!;

        Assert.Equal(SqlJsonOperator.DoubleArrow, json.Operator);
        Assert.NotNull(json.Left.Column);
        Assert.Equal("data", json.Left.Column!.ColumnName);
        Assert.NotNull(json.Right.Value);
        Assert.Equal("name", json.Right.Value!.String);
    }

    [Fact]
    public void JsonDoubleArrow_ColumnAndIntegerIndex()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data ->> 1");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.JsonExpr);
        Assert.Equal(SqlJsonOperator.DoubleArrow, expression.JsonExpr!.Operator);
    }

    // ── #> operator (JSON path, returns JSON) ────────────────────────────

    [Fact]
    public void JsonHashArrow_ColumnAndPath()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data #> '{a,b}'");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.JsonExpr);
        var json = expression.JsonExpr!;

        Assert.Equal(SqlJsonOperator.HashArrow, json.Operator);
        Assert.NotNull(json.Left.Column);
        Assert.Equal("data", json.Left.Column!.ColumnName);
        Assert.NotNull(json.Right.Value);
        Assert.Equal("{a,b}", json.Right.Value!.String);
    }

    // ── #>> operator (JSON path, returns text) ───────────────────────────

    [Fact]
    public void JsonHashDoubleArrow_ColumnAndPath()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data #>> '{a,b,c}'");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.JsonExpr);
        var json = expression.JsonExpr!;

        Assert.Equal(SqlJsonOperator.HashDoubleArrow, json.Operator);
        Assert.NotNull(json.Left.Column);
        Assert.Equal("data", json.Left.Column!.ColumnName);
        Assert.NotNull(json.Right.Value);
        Assert.Equal("{a,b,c}", json.Right.Value!.String);
    }

    // ── @> containment operator ──────────────────────────────────────────

    [Fact]
    public void JsonContains_ColumnAndLiteral()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data @> '{\"key\": \"value\"}'");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.JsonExpr);
        Assert.Equal(SqlJsonOperator.Contains, expression.JsonExpr!.Operator);
        Assert.NotNull(expression.JsonExpr.Left.Column);
        Assert.Equal("data", expression.JsonExpr.Left.Column!.ColumnName);
    }

    // ── <@ contained-by operator ─────────────────────────────────────────

    [Fact]
    public void JsonContainedBy_ColumnAndLiteral()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data <@ '{\"key\": \"value\"}'");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.JsonExpr);
        Assert.Equal(SqlJsonOperator.ContainedBy, expression.JsonExpr!.Operator);
    }

    // ── ? key exists operator ────────────────────────────────────────────

    [Fact]
    public void JsonKeyExists_ColumnAndKey()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data ? 'key'");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.JsonExpr);
        var json = expression.JsonExpr!;

        Assert.Equal(SqlJsonOperator.KeyExists, json.Operator);
        Assert.NotNull(json.Left.Column);
        Assert.Equal("data", json.Left.Column!.ColumnName);
        Assert.NotNull(json.Right.Value);
        Assert.Equal("key", json.Right.Value!.String);
    }

    // ── ?| any key exists operator ───────────────────────────────────────

    [Fact]
    public void JsonAnyKeyExists_ColumnAndArray()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data ?| '{a,b}'");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.JsonExpr);
        Assert.Equal(SqlJsonOperator.AnyKeyExists, expression.JsonExpr!.Operator);
    }

    // ── ?& all keys exist operator ───────────────────────────────────────

    [Fact]
    public void JsonAllKeysExist_ColumnAndArray()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data ?& '{a,b}'");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.JsonExpr);
        Assert.Equal(SqlJsonOperator.AllKeysExist, expression.JsonExpr!.Operator);
    }

    // ── Chained JSON access ──────────────────────────────────────────────

    [Fact]
    public void JsonArrow_Chained()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data -> 'a' -> 'b'");
        var expression = grammar.Create(node);

        // Should parse as (data -> 'a') -> 'b' (left-associative)
        Assert.NotNull(expression.JsonExpr);
        var outer = expression.JsonExpr!;
        Assert.Equal(SqlJsonOperator.Arrow, outer.Operator);
        Assert.NotNull(outer.Right.Value);
        Assert.Equal("b", outer.Right.Value!.String);

        // Inner: data -> 'a'
        Assert.NotNull(outer.Left.JsonExpr);
        var inner = outer.Left.JsonExpr!;
        Assert.Equal(SqlJsonOperator.Arrow, inner.Operator);
        Assert.NotNull(inner.Left.Column);
        Assert.Equal("data", inner.Left.Column!.ColumnName);
        Assert.NotNull(inner.Right.Value);
        Assert.Equal("a", inner.Right.Value!.String);
    }

    [Fact]
    public void JsonArrow_ThenDoubleArrow()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data -> 'a' ->> 'b'");
        var expression = grammar.Create(node);

        // Outer: ->>
        Assert.NotNull(expression.JsonExpr);
        Assert.Equal(SqlJsonOperator.DoubleArrow, expression.JsonExpr!.Operator);

        // Inner: ->
        Assert.NotNull(expression.JsonExpr.Left.JsonExpr);
        Assert.Equal(SqlJsonOperator.Arrow, expression.JsonExpr.Left.JsonExpr!.Operator);
    }

    // ── ToExpressionString ───────────────────────────────────────────────

    [Fact]
    public void JsonArrow_ToExpressionString()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data -> 'key'");
        var expression = grammar.Create(node);

        Assert.Equal("data -> 'key'", expression.ToExpressionString());
    }

    [Fact]
    public void JsonDoubleArrow_ToExpressionString()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data ->> 'name'");
        var expression = grammar.Create(node);

        Assert.Equal("data ->> 'name'", expression.ToExpressionString());
    }

    [Fact]
    public void JsonHashArrow_ToExpressionString()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data #> '{a,b}'");
        var expression = grammar.Create(node);

        Assert.Equal("data #> '{a,b}'", expression.ToExpressionString());
    }

    [Fact]
    public void JsonHashDoubleArrow_ToExpressionString()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data #>> '{a,b}'");
        var expression = grammar.Create(node);

        Assert.Equal("data #>> '{a,b}'", expression.ToExpressionString());
    }

    [Fact]
    public void JsonContains_ToExpressionString()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data @> '{}'");
        var expression = grammar.Create(node);

        Assert.Equal("data @> '{}'", expression.ToExpressionString());
    }

    [Fact]
    public void JsonKeyExists_ToExpressionString()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data ? 'key'");
        var expression = grammar.Create(node);

        Assert.Equal("data ? 'key'", expression.ToExpressionString());
    }
}
