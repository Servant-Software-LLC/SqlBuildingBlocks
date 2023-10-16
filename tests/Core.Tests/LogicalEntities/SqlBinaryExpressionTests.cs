using SqlBuildingBlocks.LogicalEntities;
using System.Data;
using System.Linq.Expressions;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.LogicalEntities;

public class SqlBinaryExpressionTests
{
    [Fact]
    public void Expression_CompareIntValueAndStringColumn()
    {
        const string databaseName = "MyDB";
        SqlTable locationsTable = new(databaseName, "locations");

        SqlColumn locationsIdColumn = new(databaseName, locationsTable.TableName, "id")
        {
            ColumnType = typeof(string),
            TableRef = locationsTable
        };
        SqlColumnRef locationsIdColumnRef = new(null, null, "id") { Column = locationsIdColumn };
        SqlBinaryExpression sqlBinaryExpression = new (new(locationsIdColumnRef), SqlBinaryOperator.Equal, new(new SqlLiteralValue(1)));


        //Act
        var expression = sqlBinaryExpression.GetExpression(new Dictionary<SqlTable, DataRow>(), locationsTable,
                                                           Expression.Parameter(typeof(DataRow), "dataRow"));

        //Assert
        Assert.NotNull(expression);
    }

    [Fact]
    public void Expression_CompareIntAndDecimal()
    {
        const string databaseName = "MyDB";
        SqlTable locationsTable = new(databaseName, "locations");

        SqlColumn locationsIdColumn = new(databaseName, locationsTable.TableName, "id")
        {
            ColumnType = typeof(decimal),
            TableRef = locationsTable
        };
        SqlColumnRef locationsIdColumnRef = new(null, null, "id") { Column = locationsIdColumn };
        SqlBinaryExpression sqlBinaryExpression = new(new(locationsIdColumnRef), SqlBinaryOperator.Equal, new(new SqlLiteralValue(1)));


        //Act
        var expression = sqlBinaryExpression.GetExpression(new Dictionary<SqlTable, DataRow>(), locationsTable,
                                                           Expression.Parameter(typeof(DataRow), "dataRow"));

        //Assert
        Assert.NotNull(expression);

    }

    [Fact]
    public void ToExpressionString_DoesntIncludeDatabaseOrTableNameOfTableReference() 
    {
        const string databaseName = "MyDB";
        SqlTable locationsTable = new(databaseName, "locations");

        SqlColumn locationsIdColumn = new(databaseName, locationsTable.TableName, "id")
        {
            ColumnType = typeof(decimal),
            TableRef = locationsTable
        };
        SqlColumnRef locationsIdColumnRef = new(null, null, "id") { Column = locationsIdColumn };
        SqlBinaryExpression sqlBinaryExpression = new(new(locationsIdColumnRef), SqlBinaryOperator.Equal, new(new SqlLiteralValue(1)));

        //Act
        var expressionString = sqlBinaryExpression.ToExpressionString();

        //Assert
        Assert.Equal("id = 1", expressionString);
    }
}
