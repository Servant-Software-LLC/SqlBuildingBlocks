using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.LogicalEntities;

public class SqlLiteralValueTests
{
    [Fact]
    public void GetExpression_IntVsInt()
    {
        // Setup
        SqlLiteralValue sqlLiteralValue = new(1);
        SqlLiteralValue sqlLiteralValueOther = new(2);

        // Act
        var expression = sqlLiteralValue.GetExpression(new(sqlLiteralValueOther));

        // Assert        
        Assert.NotNull(expression);
    }
}
