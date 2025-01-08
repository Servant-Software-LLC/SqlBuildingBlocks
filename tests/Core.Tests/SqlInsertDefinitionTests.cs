using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using System.Data.Common;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class SqlInsertDefinitionTests
{
    [Fact]
    public void ResolveParameters_Simple()
    {
        // Setup
        SqlInsertDefinition sqlInsertDefinition = new();
        sqlInsertDefinition.Columns.Add(new(null, "Blogs", "Url"));
        sqlInsertDefinition.Table = new(null, "Blogs");
        sqlInsertDefinition.Values = new List<SqlExpression>() { new SqlExpression(new SqlParameter("p0")) };

        DbParameterCollection parameters = new FakeParameterCollection();
        DbParameter testParameter = new FakeDbParameter
        {
            ParameterName = "@p0",
            Value = "http://blogs.msdn.com/adonet"
        };
        parameters.Add(testParameter);

        // Act
        sqlInsertDefinition.ResolveParameters(parameters);

        // Assert
        Assert.Single(sqlInsertDefinition.Values);
        var valueExpression = sqlInsertDefinition.Values[0];
        Assert.Null(valueExpression.Parameter);
        Assert.NotNull(valueExpression.Value);
        var literalValue = valueExpression.Value;
        Assert.Equal(testParameter.Value, literalValue.String);
    }
}
