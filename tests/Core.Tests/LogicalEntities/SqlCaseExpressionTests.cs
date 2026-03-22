using SqlBuildingBlocks.LogicalEntities;
using System.Data;
using System.Linq.Expressions;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.LogicalEntities;

public class SqlCaseExpressionTests
{
    [Fact]
    public void GetExpression_SingleWhenWithElse()
    {
        const string databaseName = "MyDB";
        SqlTable table = new(databaseName, "orders");

        SqlColumn column = new(databaseName, table.TableName, "quantity")
        {
            ColumnType = typeof(int),
            TableRef = table
        };
        SqlColumnRef columnRef = new(null, null, "quantity") { Column = column };

        // CASE WHEN quantity > 10 THEN 'large' ELSE 'small' END
        var condition = new SqlBinaryExpression(
            new SqlExpression(columnRef),
            SqlBinaryOperator.GreaterThan,
            new SqlExpression(new SqlLiteralValue(10))
        );

        var caseExpr = new SqlCaseExpression(
            new List<(SqlExpression, SqlExpression)>
            {
                (new SqlExpression(condition), new SqlExpression(new SqlLiteralValue("large")))
            },
            new SqlExpression(new SqlLiteralValue("small"))
        );

        var param = Expression.Parameter(typeof(DataRow), "dataRow");
        var expression = caseExpr.GetExpression(new Dictionary<SqlTable, DataRow>(), table, param);

        Assert.NotNull(expression);
        Assert.IsAssignableFrom<ConditionalExpression>(expression);
    }

    [Fact]
    public void GetExpression_MultipleWhenClauses()
    {
        const string databaseName = "MyDB";
        SqlTable table = new(databaseName, "orders");

        SqlColumn column = new(databaseName, table.TableName, "quantity")
        {
            ColumnType = typeof(int),
            TableRef = table
        };
        SqlColumnRef columnRef = new(null, null, "quantity") { Column = column };

        // CASE WHEN quantity > 100 THEN 'huge' WHEN quantity > 10 THEN 'large' ELSE 'small' END
        var cond1 = new SqlBinaryExpression(
            new SqlExpression(columnRef),
            SqlBinaryOperator.GreaterThan,
            new SqlExpression(new SqlLiteralValue(100))
        );
        var cond2 = new SqlBinaryExpression(
            new SqlExpression(columnRef),
            SqlBinaryOperator.GreaterThan,
            new SqlExpression(new SqlLiteralValue(10))
        );

        var caseExpr = new SqlCaseExpression(
            new List<(SqlExpression, SqlExpression)>
            {
                (new SqlExpression(cond1), new SqlExpression(new SqlLiteralValue("huge"))),
                (new SqlExpression(cond2), new SqlExpression(new SqlLiteralValue("large")))
            },
            new SqlExpression(new SqlLiteralValue("small"))
        );

        var param = Expression.Parameter(typeof(DataRow), "dataRow");
        var expression = caseExpr.GetExpression(new Dictionary<SqlTable, DataRow>(), table, param);

        Assert.NotNull(expression);
        // Outer is a conditional; its IfFalse branch is also a conditional (nested WHEN clauses)
        var conditional = Assert.IsAssignableFrom<ConditionalExpression>(expression);
        Assert.IsAssignableFrom<ConditionalExpression>(conditional.IfFalse);
    }

    [Fact]
    public void GetExpression_NoElse_DefaultsToNull()
    {
        const string databaseName = "MyDB";
        SqlTable table = new(databaseName, "orders");

        SqlColumn column = new(databaseName, table.TableName, "quantity")
        {
            ColumnType = typeof(int),
            TableRef = table
        };
        SqlColumnRef columnRef = new(null, null, "quantity") { Column = column };

        // CASE WHEN quantity > 10 THEN 'large' END  (no ELSE)
        var condition = new SqlBinaryExpression(
            new SqlExpression(columnRef),
            SqlBinaryOperator.GreaterThan,
            new SqlExpression(new SqlLiteralValue(10))
        );

        var caseExpr = new SqlCaseExpression(
            new List<(SqlExpression, SqlExpression)>
            {
                (new SqlExpression(condition), new SqlExpression(new SqlLiteralValue("large")))
            },
            elseResult: null
        );

        var param = Expression.Parameter(typeof(DataRow), "dataRow");
        var expression = caseExpr.GetExpression(new Dictionary<SqlTable, DataRow>(), table, param);

        Assert.NotNull(expression);
        var conditional = Assert.IsAssignableFrom<ConditionalExpression>(expression);
        // The IfFalse branch should be a null constant (converted to string to match the THEN branch)
        Assert.Equal(typeof(string), conditional.Type);
    }

    [Fact]
    public void GetExpression_IntResults()
    {
        const string databaseName = "MyDB";
        SqlTable table = new(databaseName, "orders");

        SqlColumn column = new(databaseName, table.TableName, "status")
        {
            ColumnType = typeof(int),
            TableRef = table
        };
        SqlColumnRef columnRef = new(null, null, "status") { Column = column };

        // CASE WHEN status = 1 THEN 100 ELSE 0 END
        var condition = new SqlBinaryExpression(
            new SqlExpression(columnRef),
            SqlBinaryOperator.Equal,
            new SqlExpression(new SqlLiteralValue(1))
        );

        var caseExpr = new SqlCaseExpression(
            new List<(SqlExpression, SqlExpression)>
            {
                (new SqlExpression(condition), new SqlExpression(new SqlLiteralValue(100)))
            },
            new SqlExpression(new SqlLiteralValue(0))
        );

        var param = Expression.Parameter(typeof(DataRow), "dataRow");
        var expression = caseExpr.GetExpression(new Dictionary<SqlTable, DataRow>(), table, param);

        Assert.NotNull(expression);
        Assert.IsAssignableFrom<ConditionalExpression>(expression);
    }

    [Fact]
    public void GetExpression_ViaParentSqlExpression()
    {
        const string databaseName = "MyDB";
        SqlTable table = new(databaseName, "orders");

        SqlColumn column = new(databaseName, table.TableName, "quantity")
        {
            ColumnType = typeof(int),
            TableRef = table
        };
        SqlColumnRef columnRef = new(null, null, "quantity") { Column = column };

        var condition = new SqlBinaryExpression(
            new SqlExpression(columnRef),
            SqlBinaryOperator.GreaterThan,
            new SqlExpression(new SqlLiteralValue(10))
        );

        var caseExpr = new SqlCaseExpression(
            new List<(SqlExpression, SqlExpression)>
            {
                (new SqlExpression(condition), new SqlExpression(new SqlLiteralValue("large")))
            },
            new SqlExpression(new SqlLiteralValue("small"))
        );

        // Access via SqlExpression wrapper (the dispatch path from SqlExpression.GetExpression)
        var sqlExpr = new SqlExpression(caseExpr);
        var param = Expression.Parameter(typeof(DataRow), "dataRow");
        var expression = sqlExpr.GetExpression(new Dictionary<SqlTable, DataRow>(), table, param,
            new SqlExpression(new SqlLiteralValue(0)));

        Assert.NotNull(expression);
        Assert.IsAssignableFrom<ConditionalExpression>(expression);
    }

    [Fact]
    public void ToExpressionString_SingleWhenWithElse()
    {
        var condition = new SqlExpression(new SqlBinaryExpression(
            new SqlExpression(new SqlColumnRef(null, null, "x") { Column = new SqlColumn("db", "t", "x") }),
            SqlBinaryOperator.GreaterThan,
            new SqlExpression(new SqlLiteralValue(10))
        ));
        var result = new SqlExpression(new SqlLiteralValue("yes"));
        var elseResult = new SqlExpression(new SqlLiteralValue("no"));

        var caseExpr = new SqlCaseExpression(
            new List<(SqlExpression, SqlExpression)> { (condition, result) },
            elseResult
        );

        var str = caseExpr.ToExpressionString();
        Assert.Contains("CASE", str);
        Assert.Contains("WHEN", str);
        Assert.Contains("THEN", str);
        Assert.Contains("ELSE", str);
        Assert.Contains("END", str);
    }

    [Fact]
    public void ToExpressionString_NoElse()
    {
        var condition = new SqlExpression(new SqlBinaryExpression(
            new SqlExpression(new SqlColumnRef(null, null, "x") { Column = new SqlColumn("db", "t", "x") }),
            SqlBinaryOperator.GreaterThan,
            new SqlExpression(new SqlLiteralValue(10))
        ));
        var result = new SqlExpression(new SqlLiteralValue("yes"));

        var caseExpr = new SqlCaseExpression(
            new List<(SqlExpression, SqlExpression)> { (condition, result) },
            elseResult: null
        );

        var str = caseExpr.ToExpressionString();
        Assert.Contains("CASE", str);
        Assert.Contains("WHEN", str);
        Assert.DoesNotContain("ELSE", str);
        Assert.Contains("END", str);
    }
}
