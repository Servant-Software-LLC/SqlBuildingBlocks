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

    // ── CASE WHEN/THEN/ELSE/END ───────────────────────────────────────────

    [Fact]
    public void CaseWhen_SingleClause_NoElse()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "CASE WHEN status = 1 THEN 'active' END");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CaseExpr);
        var caseExpr = expression.CaseExpr!;

        Assert.Single(caseExpr.WhenClauses);
        Assert.Null(caseExpr.ElseResult);

        var (condition, result) = caseExpr.WhenClauses[0];
        Assert.NotNull(condition.BinExpr);
        Assert.Equal("status", condition.BinExpr!.Left.Column!.ColumnName);
        Assert.NotNull(result.Value);
        Assert.Equal("active", result.Value!.String);
    }

    [Fact]
    public void CaseWhen_MultipleClausesWithElse()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "CASE WHEN score >= 90 THEN 'A' WHEN score >= 80 THEN 'B' ELSE 'C' END");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CaseExpr);
        var caseExpr = expression.CaseExpr!;

        Assert.Equal(2, caseExpr.WhenClauses.Count);
        Assert.NotNull(caseExpr.ElseResult);
        Assert.Equal("C", caseExpr.ElseResult!.Value!.String);

        var (cond1, res1) = caseExpr.WhenClauses[0];
        Assert.Equal(SqlBinaryOperator.GreaterThanEqual, cond1.BinExpr!.Operator);
        Assert.Equal("A", res1.Value!.String);

        var (cond2, res2) = caseExpr.WhenClauses[1];
        Assert.Equal(SqlBinaryOperator.GreaterThanEqual, cond2.BinExpr!.Operator);
        Assert.Equal("B", res2.Value!.String);
    }

    [Fact]
    public void CaseWhen_ToExpressionString_NoElse()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "CASE WHEN x = 1 THEN 'yes' END");
        var expression = grammar.Create(node);

        Assert.Equal("CASE WHEN x = 1 THEN 'yes' END", expression.ToExpressionString());
    }

    [Fact]
    public void CaseWhen_ToExpressionString_WithElse()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "CASE WHEN x = 1 THEN 'yes' ELSE 'no' END");
        var expression = grammar.Create(node);

        Assert.Equal("CASE WHEN x = 1 THEN 'yes' ELSE 'no' END", expression.ToExpressionString());
    }

    [Fact]
    public void Exists_Subquery()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "EXISTS (SELECT id FROM orders WHERE amount > 100)");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.ExistsExpr);
        Assert.False(expression.ExistsExpr!.IsNegated);
        Assert.Equal("orders", expression.ExistsExpr.SelectDefinition.Table!.TableName);
    }

    [Fact]
    public void NotExists_Subquery()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "NOT EXISTS (SELECT id FROM orders WHERE amount > 100)");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.ExistsExpr);
        Assert.True(expression.ExistsExpr!.IsNegated);
        Assert.Equal("orders", expression.ExistsExpr.SelectDefinition.Table!.TableName);
    }

    [Fact]
    public void Exists_ToExpressionString()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "EXISTS (SELECT id FROM orders WHERE amount > 100)");
        var expression = grammar.Create(node);

        Assert.StartsWith("EXISTS (", expression.ToExpressionString());
    }

    [Fact]
    public void Exists_AcceptsVisitor()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "EXISTS (SELECT id FROM orders WHERE amount > 100)");
        var expression = grammar.Create(node);
        DetectVisitedVisitor visitor = new();

        expression.Accept(visitor);

        Assert.True(visitor.VisitedExistsExpression);
    }

    [Fact]
    public void In_Subquery_ParseTree_UsesParenthesizedSelectNode()
    {
        TestGrammar grammar = new();
        var parseTree = GrammarParser.ParseTree(grammar, "customer_id IN (SELECT id FROM orders)");

        Assert.Equal("binExpr", parseTree.Root.Term.Name);
        Assert.Equal("parSelectStmt", parseTree.Root.ChildNodes[2].Term.Name);
    }

    [Fact]
    public void Equal_ScalarSubquery_ParseTree_UsesParenthesizedSelectNode()
    {
        TestGrammar grammar = new();
        var parseTree = GrammarParser.ParseTree(grammar, "amount = (SELECT MAX(total) FROM orders)");

        Assert.Equal("binExpr", parseTree.Root.Term.Name);
        Assert.Equal("parSelectStmt", parseTree.Root.ChildNodes[2].Term.Name);
    }

    // ── NOT LIKE ──────────────────────────────────────────────────────────

    [Fact]
    public void NotLike_StringLiteral()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "name NOT LIKE 'A%'");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.BinExpr);
        var binExpr = expression.BinExpr!;

        Assert.Equal(SqlBinaryOperator.NotLike, binExpr.Operator);
        Assert.NotNull(binExpr.Left.Column);
        Assert.Equal("name", binExpr.Left.Column.ColumnName);
        Assert.NotNull(binExpr.Right!.Value);
        Assert.Equal("A%", binExpr.Right.Value.String);
    }

    [Fact]
    public void NotLike_ToExpressionString()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "name NOT LIKE 'A%'");
        var expression = grammar.Create(node);

        Assert.Equal("name NOT LIKE 'A%'", expression.ToExpressionString());
    }

    // ── NOT IN ────────────────────────────────────────────────────────────

    [Fact]
    public void NotIn_Tuple()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "status NOT IN (1, 2, 3)");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.BinExpr);
        var binExpr = expression.BinExpr!;

        Assert.Equal(SqlBinaryOperator.NotIn, binExpr.Operator);
        Assert.NotNull(binExpr.Left.Column);
        Assert.Equal("status", binExpr.Left.Column.ColumnName);
    }

    [Fact]
    public void In_Tuple()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "status IN (1, 2, 3)");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.BinExpr);
        var binExpr = expression.BinExpr!;

        Assert.Equal(SqlBinaryOperator.In, binExpr.Operator);
        Assert.NotNull(binExpr.Left.Column);
        Assert.Equal("status", binExpr.Left.Column.ColumnName);
    }

    [Fact]
    public void In_Tuple_ParseTree_UsesTupleNode()
    {
        TestGrammar grammar = new();
        var parseTree = GrammarParser.ParseTree(grammar, "status IN (1, 2, 3)");

        Assert.Equal("binExpr", parseTree.Root.Term.Name);
        Assert.Equal("tuple", parseTree.Root.ChildNodes[2].Term.Name);
    }

    [Fact]
    public void In_Subquery_ParsesWithoutTupleConflict()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "status IN (SELECT status FROM orders)");

        Assert.Equal("binExpr", node.Term.Name);
    }

    [Fact]
    public void Comparison_WithScalarSubquery_Parses()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "amount = (SELECT MAX(amount) FROM orders)");

        Assert.Equal("binExpr", node.Term.Name);
    }

    // ── CAST ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Cast_ColumnToVarchar()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "CAST(order_id AS VARCHAR(50))");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        var cast = expression.CastExpr!;

        Assert.NotNull(cast.Expression.Column);
        Assert.Equal("order_id", cast.Expression.Column.ColumnName);

        Assert.Equal("VARCHAR", cast.DataType.Name);
        Assert.Equal(50, cast.DataType.Length);
    }

    [Fact]
    public void Cast_ColumnToInt()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "CAST(price AS INT)");
        var expression = grammar.Create(node);

        Assert.NotNull(expression.CastExpr);
        var cast = expression.CastExpr!;

        Assert.NotNull(cast.Expression.Column);
        Assert.Equal("price", cast.Expression.Column.ColumnName);

        Assert.Equal("INT", cast.DataType.Name);
        Assert.Null(cast.DataType.Length);
    }

    [Fact]
    public void Cast_ToExpressionString_WithLength()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "CAST(order_id AS VARCHAR(50))");
        var expression = grammar.Create(node);

        Assert.Equal("CAST(order_id AS VARCHAR(50))", expression.ToExpressionString());
    }

    [Fact]
    public void Cast_ToExpressionString_NoParams()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "CAST(price AS INT)");
        var expression = grammar.Create(node);

        Assert.Equal("CAST(price AS INT)", expression.ToExpressionString());
    }

}
