using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class UpdateStmtTests
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

            UpdateStmt updateStmt = new(this, id, literalValue, parameter, funcCall, tableName, whereClauseOpt, joinChainOpt);

            Root = updateStmt;
        }

        public virtual SqlUpdateDefinition Create(ParseTreeNode updateStmt) =>
            ((UpdateStmt)Root).Create(updateStmt);
    }

    [Fact]
    public void Update_WithWhere()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE locations SET zip = 32655 WHERE city = 'Boston'");
        var sqlUpdateDefinition = grammar.Create(node);

        //Assert - Table
        Assert.NotNull(sqlUpdateDefinition.Table);
        Assert.Equal("locations", sqlUpdateDefinition.Table.TableName);

        //Assert - SET
        var assignments = sqlUpdateDefinition.Assignments;
        Assert.Single(assignments);
        Assert.Equal("zip", assignments[0].Column.ColumnName);
        Assert.Equal(32655, assignments[0].Value.Int);

        //Assert - WHERE
        var whereClause = sqlUpdateDefinition.WhereClause;
        Assert.NotNull(whereClause);
        var whereClauseBin = whereClause!.BinExpr;
        Assert.NotNull(whereClauseBin);
        Assert.Equal("city", whereClauseBin!.Left.Column.ColumnName);
        Assert.Equal("Boston", whereClauseBin.Right!.Value.String);

        Assert.Null(sqlUpdateDefinition.Returning);
    }

    [Fact]
    public void Update_SetWithParameter()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE locations SET zip = @Zip");
        var sqlUpdateDefinition = grammar.Create(node);

        //Assert - Table
        Assert.NotNull(sqlUpdateDefinition.Table);
        Assert.Equal("locations", sqlUpdateDefinition.Table.TableName);

        //Assert - SET
        var assignments = sqlUpdateDefinition.Assignments;
        Assert.Single(assignments);
        Assert.Equal("zip", assignments[0].Column.ColumnName);
        Assert.Equal("Zip", assignments[0].Parameter.Name);
    }

    [Fact]
    public void Update_SetWithFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE locations SET zip = ROW_COUNT()");
        var sqlUpdateDefinition = grammar.Create(node);

        //Assert - Table
        Assert.NotNull(sqlUpdateDefinition.Table);
        Assert.Equal("locations", sqlUpdateDefinition.Table.TableName);

        //Assert - SET
        var assignments = sqlUpdateDefinition.Assignments;
        Assert.Single(assignments);
        Assert.Equal("zip", assignments[0].Column.ColumnName);
        Assert.Equal("ROW_COUNT", assignments[0].Function.FunctionName);
    }

    [Fact]
    public void Update_WithIntReturingClause()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE SomeSetting SET SomeProperty = @p0 WHERE Id = @p1 RETURNING 1");
        var sqlUpdateDefinition = grammar.Create(node);

        //Assert
        Assert.NotNull(sqlUpdateDefinition.Returning);
        Assert.Equal(1, sqlUpdateDefinition.Returning.Int);
    }

    [Fact]
    public void Update_WithColumnReturingClause()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE SomeSetting SET SomeProperty = @p0 WHERE Id = @p1 RETURNING Id");
        var sqlUpdateDefinition = grammar.Create(node);

        //Assert
        Assert.NotNull(sqlUpdateDefinition.Returning);
        Assert.NotNull(sqlUpdateDefinition.Returning.Column);
        Assert.Equal("Id", sqlUpdateDefinition.Returning.Column.ColumnName);
    }

    [Fact]
    public void Update_WithJoinBeforeSet()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE orders o JOIN customers c ON o.customer_id = c.id SET o.status = 'vip' WHERE c.tier = 'gold'");
        var sqlUpdateDefinition = grammar.Create(node);

        Assert.NotNull(sqlUpdateDefinition.Table);
        Assert.Equal("orders", sqlUpdateDefinition.Table!.TableName);
        Assert.Equal("o", sqlUpdateDefinition.Table.TableAlias);

        Assert.Null(sqlUpdateDefinition.SourceTable);
        Assert.Single(sqlUpdateDefinition.Joins);
        Assert.Equal("customers", sqlUpdateDefinition.Joins[0].Table.TableName);
        Assert.Equal("c", sqlUpdateDefinition.Joins[0].Table.TableAlias);

        Assert.Single(sqlUpdateDefinition.Assignments);
        Assert.Equal("status", sqlUpdateDefinition.Assignments[0].Column.ColumnName);
        Assert.Equal("vip", sqlUpdateDefinition.Assignments[0].Value.String);
    }

    [Fact]
    public void Update_WithFromJoinAfterSet()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE o SET o.status = 'vip' FROM orders o JOIN customers c ON o.customer_id = c.id WHERE c.tier = 'gold'");
        var sqlUpdateDefinition = grammar.Create(node);

        Assert.NotNull(sqlUpdateDefinition.Table);
        Assert.Equal("o", sqlUpdateDefinition.Table!.TableName);

        Assert.NotNull(sqlUpdateDefinition.SourceTable);
        Assert.Equal("orders", sqlUpdateDefinition.SourceTable!.TableName);
        Assert.Equal("o", sqlUpdateDefinition.SourceTable.TableAlias);

        Assert.Single(sqlUpdateDefinition.Joins);
        Assert.Equal("customers", sqlUpdateDefinition.Joins[0].Table.TableName);
        Assert.Equal("c", sqlUpdateDefinition.Joins[0].Table.TableAlias);
    }

    [Fact]
    public void Update_SetWithArithmeticExpression()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE products SET price = price + 10 WHERE category = 'Electronics'");
        var sqlUpdateDefinition = grammar.Create(node);

        Assert.NotNull(sqlUpdateDefinition.Table);
        Assert.Equal("products", sqlUpdateDefinition.Table.TableName);

        var assignments = sqlUpdateDefinition.Assignments;
        Assert.Single(assignments);
        Assert.Equal("price", assignments[0].Column.ColumnName);

        //The value is a binary expression: price + 10
        var binExpr = assignments[0].Expression.BinExpr;
        Assert.NotNull(binExpr);
        Assert.Equal("price", binExpr!.Left.Column.ColumnName);
        Assert.Equal(SqlBinaryOperator.Plus, binExpr.Operator);
        Assert.Equal(10, binExpr.Right!.Value.Int);
    }

    [Fact]
    public void Update_SetWithColumnReference()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE my_table SET column1 = column2");
        var sqlUpdateDefinition = grammar.Create(node);

        var assignments = sqlUpdateDefinition.Assignments;
        Assert.Single(assignments);
        Assert.Equal("column1", assignments[0].Column.ColumnName);
        Assert.NotNull(assignments[0].Expression.Column);
        Assert.Equal("column2", assignments[0].Expression.Column!.ColumnName);
    }

    [Fact]
    public void Update_SetWithMultiColumnArithmetic()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE my_table SET column1 = column2 + column3");
        var sqlUpdateDefinition = grammar.Create(node);

        var assignments = sqlUpdateDefinition.Assignments;
        Assert.Single(assignments);
        Assert.Equal("column1", assignments[0].Column.ColumnName);

        var binExpr = assignments[0].Expression.BinExpr;
        Assert.NotNull(binExpr);
        Assert.Equal("column2", binExpr!.Left.Column.ColumnName);
        Assert.Equal(SqlBinaryOperator.Plus, binExpr.Operator);
        Assert.Equal("column3", binExpr.Right!.Column.ColumnName);
    }

    [Fact]
    public void Update_SetWithCaseExpression()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE my_table SET column1 = CASE WHEN column2 > 10 THEN 'High' ELSE 'Low' END");
        var sqlUpdateDefinition = grammar.Create(node);

        var assignments = sqlUpdateDefinition.Assignments;
        Assert.Single(assignments);
        Assert.Equal("column1", assignments[0].Column.ColumnName);

        var caseExpr = assignments[0].Expression.CaseExpr;
        Assert.NotNull(caseExpr);
        Assert.Single(caseExpr!.WhenClauses);

        //WHEN column2 > 10
        var whenCondition = caseExpr.WhenClauses[0].Condition.BinExpr;
        Assert.NotNull(whenCondition);
        Assert.Equal("column2", whenCondition!.Left.Column.ColumnName);
        Assert.Equal(SqlBinaryOperator.GreaterThan, whenCondition.Operator);
        Assert.Equal(10, whenCondition.Right!.Value.Int);

        //THEN 'High'
        Assert.Equal("High", caseExpr.WhenClauses[0].Result.Value.String);

        //ELSE 'Low'
        Assert.NotNull(caseExpr.ElseResult);
        Assert.Equal("Low", caseExpr.ElseResult!.Value.String);
    }

    [Fact]
    public void Update_SetWithScalarSubquery()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE my_table SET column1 = (SELECT column2 FROM other_table WHERE id = 1) WHERE id = 1");
        var sqlUpdateDefinition = grammar.Create(node);

        var assignments = sqlUpdateDefinition.Assignments;
        Assert.Single(assignments);
        Assert.Equal("column1", assignments[0].Column.ColumnName);

        var scalarSubquery = assignments[0].Expression.ScalarSubqueryExpr;
        Assert.NotNull(scalarSubquery);
        Assert.NotNull(scalarSubquery!.SelectDefinition);
        Assert.Equal("other_table", scalarSubquery.SelectDefinition.Table.TableName);
    }

}
