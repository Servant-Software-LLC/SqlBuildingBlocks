using SqlBuildingBlocks.LogicalEntities;
using System.Data;
using System.Linq.Expressions;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.LogicalEntities;

public class SqlBetweenExpressionTests
{
    [Fact]
    public void GetExpression_IntColumnBetweenIntLiterals()
    {
        const string databaseName = "MyDB";
        SqlTable table = new(databaseName, "orders");

        SqlColumn column = new(databaseName, table.TableName, "quantity")
        {
            ColumnType = typeof(int),
            TableRef = table
        };
        SqlColumnRef columnRef = new(null, null, "quantity") { Column = column };

        var betweenExpr = new SqlBetweenExpression(
            new SqlExpression(columnRef),
            new SqlExpression(new SqlLiteralValue(10)),
            new SqlExpression(new SqlLiteralValue(50)),
            isNegated: false
        );

        var param = Expression.Parameter(typeof(DataRow), "dataRow");
        var expression = betweenExpr.GetExpression(new Dictionary<SqlTable, DataRow>(), table, param);

        Assert.NotNull(expression);
    }

    [Fact]
    public void GetExpression_NotBetween_ReturnsNegatedExpression()
    {
        const string databaseName = "MyDB";
        SqlTable table = new(databaseName, "orders");

        SqlColumn column = new(databaseName, table.TableName, "quantity")
        {
            ColumnType = typeof(int),
            TableRef = table
        };
        SqlColumnRef columnRef = new(null, null, "quantity") { Column = column };

        var betweenExpr = new SqlBetweenExpression(
            new SqlExpression(columnRef),
            new SqlExpression(new SqlLiteralValue(10)),
            new SqlExpression(new SqlLiteralValue(50)),
            isNegated: true
        );

        var param = Expression.Parameter(typeof(DataRow), "dataRow");
        var expression = betweenExpr.GetExpression(new Dictionary<SqlTable, DataRow>(), table, param);

        Assert.NotNull(expression);
        // NOT BETWEEN wraps in a Not expression
        Assert.IsType<UnaryExpression>(expression);
        Assert.Equal(ExpressionType.Not, expression.NodeType);
    }

    [Fact]
    public void GetExpression_StringColumnBetweenStringLiterals()
    {
        const string databaseName = "MyDB";
        SqlTable table = new(databaseName, "orders");

        SqlColumn column = new(databaseName, table.TableName, "order_date")
        {
            ColumnType = typeof(string),
            TableRef = table
        };
        SqlColumnRef columnRef = new(null, null, "order_date") { Column = column };

        var betweenExpr = new SqlBetweenExpression(
            new SqlExpression(columnRef),
            new SqlExpression(new SqlLiteralValue("2024-01-01")),
            new SqlExpression(new SqlLiteralValue("2024-12-31")),
            isNegated: false
        );

        var param = Expression.Parameter(typeof(DataRow), "dataRow");
        var expression = betweenExpr.GetExpression(new Dictionary<SqlTable, DataRow>(), table, param);

        Assert.NotNull(expression);
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

        var betweenExpr = new SqlBetweenExpression(
            new SqlExpression(columnRef),
            new SqlExpression(new SqlLiteralValue(10)),
            new SqlExpression(new SqlLiteralValue(50)),
            isNegated: false
        );

        // Access via SqlExpression wrapper (the path from SqlExpression.GetExpression)
        var sqlExpr = new SqlExpression(betweenExpr);
        var param = Expression.Parameter(typeof(DataRow), "dataRow");
        var expression = sqlExpr.GetExpression(new Dictionary<SqlTable, DataRow>(), table, param,
            new SqlExpression(new SqlLiteralValue(0)));

        Assert.NotNull(expression);
    }
}
