using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.PostgreSQL.Tests;

public class ExprTests
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

            Root = expr;
        }

        public SqlExpression Create(ParseTreeNode expression) =>
            ((PostgreSQL.Expr)Root).Create(expression);
    }

    // ── Basic :: cast ──────────────────────────────────────────────────────

    [Fact]
    public void PgCast_ColumnToInteger()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "price::INTEGER");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        var cast = expression.CastExpr!;

        Assert.NotNull(cast.Expression.Column);
        Assert.Equal("price", cast.Expression.Column.ColumnName);
        Assert.Equal("INTEGER", cast.DataType.Name);
    }

    [Fact]
    public void PgCast_ColumnToText()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "col::TEXT");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        var cast = expression.CastExpr!;

        Assert.NotNull(cast.Expression.Column);
        Assert.Equal("col", cast.Expression.Column.ColumnName);
        Assert.Equal("TEXT", cast.DataType.Name);
    }

    [Fact]
    public void PgCast_StringLiteralToDate()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "'2023-01-01'::DATE");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        var cast = expression.CastExpr!;

        Assert.NotNull(cast.Expression.Value);
        Assert.Equal("2023-01-01", cast.Expression.Value.String);
        Assert.Equal("DATE", cast.DataType.Name);
    }

    [Fact]
    public void PgCast_ColumnToVarcharWithLength()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "name::VARCHAR(100)");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        var cast = expression.CastExpr!;

        Assert.NotNull(cast.Expression.Column);
        Assert.Equal("name", cast.Expression.Column.ColumnName);
        Assert.Equal("VARCHAR", cast.DataType.Name);
        Assert.Equal(100, cast.DataType.Length);
    }

    [Fact]
    public void PgCast_ColumnToNumericWithPrecisionScale()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "amount::NUMERIC(10,2)");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        var cast = expression.CastExpr!;

        Assert.NotNull(cast.Expression.Column);
        Assert.Equal("amount", cast.Expression.Column.ColumnName);
        Assert.Equal("NUMERIC", cast.DataType.Name);
        Assert.Equal(10, cast.DataType.Precision);
        Assert.Equal(2, cast.DataType.Scale);
    }

    [Fact]
    public void PgCast_IntLiteralToBoolean()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "1::BOOLEAN");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        var cast = expression.CastExpr!;

        Assert.NotNull(cast.Expression.Value);
        Assert.Equal("BOOLEAN", cast.DataType.Name);
    }

    [Fact]
    public void PgCast_ColumnToFloat()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "value::FLOAT");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        var cast = expression.CastExpr!;

        Assert.NotNull(cast.Expression.Column);
        Assert.Equal("value", cast.Expression.Column.ColumnName);
        Assert.Equal("FLOAT", cast.DataType.Name);
    }

    // ── Chained casts ──────────────────────────────────────────────────────

    [Fact]
    public void PgCast_ChainedCast_TextThenVarchar()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "col::TEXT::VARCHAR(50)");
        var expression = grammar.Create(node);

        // Outer cast: ::VARCHAR(50)
        Assert.NotNull(expression.CastExpr);
        var outerCast = expression.CastExpr!;
        Assert.Equal("VARCHAR", outerCast.DataType.Name);
        Assert.Equal(50, outerCast.DataType.Length);

        // Inner cast: ::TEXT
        Assert.NotNull(outerCast.Expression.CastExpr);
        var innerCast = outerCast.Expression.CastExpr!;
        Assert.Equal("TEXT", innerCast.DataType.Name);

        Assert.NotNull(innerCast.Expression.Column);
        Assert.Equal("col", innerCast.Expression.Column.ColumnName);
    }

    [Fact]
    public void PgCast_ChainedCast_IntegerThenText()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "value::INTEGER::TEXT");
        var expression = grammar.Create(node);

        // Outer cast: ::TEXT
        Assert.NotNull(expression.CastExpr);
        var outerCast = expression.CastExpr!;
        Assert.Equal("TEXT", outerCast.DataType.Name);

        // Inner cast: ::INTEGER
        Assert.NotNull(outerCast.Expression.CastExpr);
        var innerCast = outerCast.Expression.CastExpr!;
        Assert.Equal("INTEGER", innerCast.DataType.Name);

        Assert.NotNull(innerCast.Expression.Column);
        Assert.Equal("value", innerCast.Expression.Column.ColumnName);
    }

    // ── Case insensitivity ─────────────────────────────────────────────────

    [Fact]
    public void PgCast_CaseInsensitive()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "col::integer");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        Assert.Equal("INTEGER", expression.CastExpr!.DataType.Name);
    }

    // ── :: cast produces same SqlCastExpression as CAST() ──────────────────

    [Fact]
    public void PgCast_ProducesSameCastExpression_AsFunctionSyntax()
    {
        TestGrammar grammar = new();

        // Parse :: syntax
        var pgNode = GrammarParser.Parse(grammar, "price::INT");
        var pgExpr = grammar.Create(pgNode);

        // Parse CAST() syntax
        var castNode = GrammarParser.Parse(grammar, "CAST(price AS INT)");
        var castExpr = grammar.Create(castNode);

        // Both should produce SqlCastExpression
        Assert.NotNull(pgExpr.CastExpr);
        Assert.NotNull(castExpr.CastExpr);

        // Same column and data type
        Assert.Equal(castExpr.CastExpr!.Expression.Column!.ColumnName,
                     pgExpr.CastExpr!.Expression.Column!.ColumnName);
        Assert.Equal(castExpr.CastExpr.DataType.Name,
                     pgExpr.CastExpr.DataType.Name);
    }

    // ── ToExpressionString ─────────────────────────────────────────────────

    [Fact]
    public void PgCast_ToExpressionString()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "price::INT");
        var expression = grammar.Create(node);

        // ToExpressionString uses CAST() syntax (normalized form)
        Assert.Equal("CAST(price AS INT)", expression.ToExpressionString());
    }

    [Fact]
    public void PgCast_ToExpressionString_WithLength()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "name::VARCHAR(50)");
        var expression = grammar.Create(node);

        Assert.Equal("CAST(name AS VARCHAR(50))", expression.ToExpressionString());
    }

    // ── :: cast in binary expressions ──────────────────────────────────────

    [Fact]
    public void PgCast_InBinaryExpression()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "price::NUMERIC(10,2) > 100");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.BinExpr);
        var binExpr = expression.BinExpr!;

        Assert.Equal(SqlBinaryOperator.GreaterThan, binExpr.Operator);

        // Left side should be a cast expression
        Assert.NotNull(binExpr.Left.CastExpr);
        Assert.Equal("price", binExpr.Left.CastExpr!.Expression.Column!.ColumnName);
        Assert.Equal("NUMERIC", binExpr.Left.CastExpr.DataType.Name);

        // Right side should be literal 100
        Assert.NotNull(binExpr.Right.Value);
    }

    // ── PostgreSQL-specific types ──────────────────────────────────────────

    [Fact]
    public void PgCast_ToJsonb()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "'{}' ::JSONB");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        Assert.Equal("JSONB", expression.CastExpr!.DataType.Name);
    }

    [Fact]
    public void PgCast_ToUuid()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "id::UUID");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        Assert.Equal("UUID", expression.CastExpr!.DataType.Name);
    }

    [Fact]
    public void PgCast_ToBytea()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "data::BYTEA");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        Assert.Equal("BYTEA", expression.CastExpr!.DataType.Name);
    }
}
