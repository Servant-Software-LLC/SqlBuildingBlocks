using Xunit;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.Visitors;

namespace SqlBuildingBlocks.Core.Tests;

public class ResolveParametersVisitorTests
{
    [Fact]
    public void ReplacesNamedParameters_SqlExpression()
    {
        // Arrange
        var sqlParameter = new SqlParameter("param1");
        var sqlLiteralValue = new SqlLiteralValue("value1");
        var namedParameters = new Dictionary<SqlParameter, SqlLiteralValue> { { sqlParameter, sqlLiteralValue } };
        var resolveParametersVisitor = new ResolveParametersVisitor(namedParameters);

        // Act
        var sqlExpression = new SqlExpression(sqlParameter);
        sqlExpression.Accept(resolveParametersVisitor);

        // Assert
        Assert.Null(sqlExpression.Parameter);
        Assert.Equal(sqlLiteralValue, sqlExpression.Value);
    }

    [Fact]
    public void ReplacesNamedParameters_SqlLimitOffset()
    {
        // Arrange
        SqlParameter sqlParameter1 = new("param1");
        SqlLiteralValue sqlLiteralValue1 = new(1);
        SqlParameter sqlParameter2 = new("param2");
        SqlLiteralValue sqlLiteralValue2 = new(2m);

        var namedParameters = new Dictionary<SqlParameter, SqlLiteralValue> { { sqlParameter1, sqlLiteralValue1 }, { sqlParameter2, sqlLiteralValue2 } };
        var resolveParametersVisitor = new ResolveParametersVisitor(namedParameters);

        var sqlLimitOffset = new SqlLimitOffset();
        sqlLimitOffset.RowCount = new(sqlParameter1);
        sqlLimitOffset.RowOffset = new(sqlParameter2);

        // Act
        sqlLimitOffset.Accept(resolveParametersVisitor);

        // Assert
        Assert.NotNull(sqlLimitOffset.RowCount);
        Assert.Null(sqlLimitOffset.RowCount.Parameter);
        Assert.Equal(sqlLiteralValue1.Int, sqlLimitOffset.RowCount.Value);

        Assert.NotNull(sqlLimitOffset.RowOffset);
        Assert.Null(sqlLimitOffset.RowOffset.Parameter);
        Assert.Equal((int)sqlLiteralValue2.Decimal, sqlLimitOffset.RowOffset.Value);

    }

    [Fact]
    public void ThrowsWhenNamedParameterNotInDictionary()
    {
        // Arrange
        var sqlParameter = new SqlParameter("param1");
        var namedParameters = new Dictionary<SqlParameter, SqlLiteralValue>();
        var resolveParametersVisitor = new ResolveParametersVisitor(namedParameters);

        // Act
        var sqlExpression = new SqlExpression(sqlParameter);

        // Assert
        Assert.Throws<ArgumentException>(() => sqlExpression.Accept(resolveParametersVisitor));
    }

    [Fact]
    public void ReplacesPositionalParameters()
    {
        // Arrange
        var sqlParameter = new SqlParameter();
        var sqlLiteralValue = new SqlLiteralValue("value1");
        var positionalParameterValues = new List<SqlLiteralValue> { sqlLiteralValue };
        var resolveParametersVisitor = new ResolveParametersVisitor(positionalParameterValues);

        // Act
        var sqlExpression = new SqlExpression(sqlParameter);
        sqlExpression.Accept(resolveParametersVisitor);

        // Assert
        Assert.Null(sqlExpression.Parameter);
        Assert.Equal(sqlLiteralValue, sqlExpression.Value);
    }

    [Fact]
    public void ThrowsWhenMorePositionalParametersThanValues()
    {
        // Arrange
        var sqlParameter = new SqlParameter();
        var positionalParameterValues = new List<SqlLiteralValue>();
        var resolveParametersVisitor = new ResolveParametersVisitor(positionalParameterValues);

        // Act
        var sqlExpression = new SqlExpression(sqlParameter);

        // Assert
        Assert.Throws<ArgumentException>(() => sqlExpression.Accept(resolveParametersVisitor));
    }
}
