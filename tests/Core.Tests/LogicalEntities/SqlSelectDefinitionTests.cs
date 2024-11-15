using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using System.Data.Common;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.LogicalEntities;

public class SqlSelectDefinitionTests
{
    [Fact]
    public void ResolveParameters_DbParameterCollection_WithSelectStatement()
    {
        // Setup
        SqlSelectDefinition sqlSelectDefinition = new();
        sqlSelectDefinition.Columns.Add(new SqlParameterColumn(new SqlParameter("Test")));
        sqlSelectDefinition.WhereClause = new(new(new SqlLiteralValue(5)), SqlBinaryOperator.Equal, new(new SqlParameter("Age")));

        DbParameterCollection parameters = new FakeParameterCollection();
        AddParameter(parameters, "Test", "Bob");
        AddParameter(parameters, "Age", 5);

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
    public void ResolveParameters_DbParameterCollection_WithInsertStatement()
    {
        // Setup
        SqlInsertDefinition sqlInsertDefinition = new();

        //Only the values are parameters in an insert statement.
        sqlInsertDefinition.Values = new List<SqlExpression>
        {
            new(new SqlParameter("TestString")),
            new(new SqlParameter("TestInt")),
            new(new SqlParameter("TestFloat")),
            new(new SqlParameter("TestDouble")),
            new(new SqlParameter("TestDecimal")),
            new(new SqlParameter("TestBool"))
        };

        DbParameterCollection parameters = new FakeParameterCollection();
        AddParameter(parameters, "TestString", "Bob");
        AddParameter(parameters, "TestInt", 5);
        AddParameter(parameters, "TestFloat", 5.1f);
        AddParameter(parameters, "TestDouble", 5.2d);
        AddParameter(parameters, "TestDecimal", 5.5m);
        AddParameter(parameters, "TestBool", true);

        // Action
        sqlInsertDefinition.ResolveParameters(parameters);

        // Assert
        Assert.Equal(6, sqlInsertDefinition.Values.Count);
        Assert.Equal("Bob", sqlInsertDefinition.Values[0].Value.String);
        Assert.Equal(5, sqlInsertDefinition.Values[1].Value.Int);
        Assert.Equal(5.1f, sqlInsertDefinition.Values[2].Value.Float);
        Assert.Equal(5.2d, sqlInsertDefinition.Values[3].Value.Double);
        Assert.Equal(5.5m, sqlInsertDefinition.Values[4].Value.Decimal);
        Assert.Equal(true, sqlInsertDefinition.Values[5].Value.Boolean);
    }

    private static void AddParameter(DbParameterCollection parameters, string parameterName, object value)
    {
        DbParameter testParameter = new FakeDbParameter
        {
            ParameterName = parameterName,
            Value = value
        };
        parameters.Add(testParameter);
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
