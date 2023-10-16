using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using System.Data.Common;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.LogicalEntities;

public class SqlSelectDefinitionTests
{
    [Fact]
    public void ResolveParameters_DbParameterCollection()
    {
        // Setup
        SqlSelectDefinition sqlSelectDefinition = new();
        sqlSelectDefinition.Columns.Add(new SqlParameterColumn(new SqlParameter("Test")));
        sqlSelectDefinition.WhereClause = new(new(new SqlLiteralValue(5)), SqlBinaryOperator.Equal, new(new SqlParameter("Age")));

        DbParameterCollection parameters = new FakeParameterCollection();
        DbParameter testParameter = new FakeDbParameter
        {
            ParameterName = "Test",
            Value = "Bob"
        };
        parameters.Add(testParameter);

        DbParameter ageParameter = new FakeDbParameter
        {
            ParameterName = "Age",
            Value = 5
        };
        parameters.Add(ageParameter);

        // Action
        sqlSelectDefinition.ResolveParameters(parameters);

        // Assert
        Assert.Single(sqlSelectDefinition.Columns);
        var firstColumn = sqlSelectDefinition.Columns[0];
        Assert.IsType<SqlLiteralValueColumn>(firstColumn);
        Assert.Equal("Bob", ((SqlLiteralValueColumn)firstColumn).Value.Value);

        var rightExpression = sqlSelectDefinition.WhereClause.Right;
        Assert.NotNull(rightExpression.Value);
        Assert.Equal(5, rightExpression.Value.Value);
    }

    [Fact]
    public void ResolveFunctions_FunctionProvider()
    {
        // Setup

        //SELECT ROW_COUNT() FROM Blogs WHERE LAST_INSERT_ID() = BlogId
        SqlSelectDefinition sqlSelectDefinition = new();
        sqlSelectDefinition.Columns.Add(new SqlFunctionColumn(new("ROW_COUNT")));
        SqlTable blogsTable = new(null, "Blogs");
        sqlSelectDefinition.Table = blogsTable;
        SqlBinaryExpression whereClause = new(new(new SqlFunction("LAST_INSERT_ID")), 
                                              SqlBinaryOperator.Equal, 
                                              new(new SqlColumnRef(null, null, "BlogId"))
                                          );
        sqlSelectDefinition.WhereClause = whereClause;

        FakeFunctionProvider functionProvider = new();

        // Act
        sqlSelectDefinition.ResolveFunctions(functionProvider);

        // Assert
        
        //ROW_COUNT() column should be a literal value now.
        Assert.Equal(1, sqlSelectDefinition.Columns.Count);
        Assert.IsType<SqlLiteralValueColumn>(sqlSelectDefinition.Columns[0]);
        var sqlLiteralValueColumn = (SqlLiteralValueColumn)sqlSelectDefinition.Columns[0];
        Assert.Equal(2, sqlLiteralValueColumn.Value.Value);

        //LAST_INSERT_ID where clause left expression should be a literal value now.
        var whereClauseLeft = sqlSelectDefinition.WhereClause.Left;
        Assert.NotNull(whereClauseLeft.Value);
        Assert.Equal(3m, whereClauseLeft.Value.Value);
    }

    [Fact]
    public void ResolveFunctions_WithAllColumn()
    {
        // Setup

        //SELECT * FROM Blogs
        SqlSelectDefinition sqlSelectDefinition = new();
        sqlSelectDefinition.Columns.Add(new SqlAllColumns());
        SqlTable blogsTable = new(null, "Blogs");
        sqlSelectDefinition.Table = blogsTable;

        FakeFunctionProvider functionProvider = new();

        // Act
        sqlSelectDefinition.ResolveFunctions(functionProvider);

        // Assert

        //All column should still be there.
        Assert.Equal(1, sqlSelectDefinition.Columns.Count);
        Assert.IsType<SqlAllColumns>(sqlSelectDefinition.Columns[0]);
    }

}
