using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.LogicalEntities;

public class SqlParameterTests
{
    [Fact]
    public void Equals_ReturnsTrue_ForIdenticalNamedParameters()
    {
        var param1 = new SqlParameter("param1");
        var param2 = new SqlParameter("param1");

        Assert.Equal(SqlParameter.ParameterType.Named, param1.Type);
        Assert.Equal(SqlParameter.ParameterType.Named, param2.Type);
        Assert.Equal(param1, param2);
    }

    [Fact]
    public void Equals_ReturnsFalse_ForNamedParametersWithDifferentNames()
    {
        var param1 = new SqlParameter("param1");
        var param2 = new SqlParameter("param2");

        Assert.NotEqual(param1, param2);
    }

    [Fact]
    public void Equals_ReturnsTrue_ForIdenticalPositionalParameters()
    {
        var param1 = new SqlParameter("?");
        var param2 = new SqlParameter("?");

        Assert.Equal(SqlParameter.ParameterType.Positional, param1.Type);
        Assert.Equal(SqlParameter.ParameterType.Positional, param2.Type);

        Assert.Equal(param1, param2);
    }

    [Fact]
    public void Equals_ReturnsFalse_ForDifferentTypesOfParameters()
    {
        var param1 = new SqlParameter("param1");
        var param2 = new SqlParameter();

        Assert.Equal(SqlParameter.ParameterType.Positional, param2.Type);

        Assert.NotEqual(param1, param2);
    }

    [Fact]
    public void GetHashCode_ReturnsSameValue_ForIdenticalNamedParameters()
    {
        var param1 = new SqlParameter("param1");
        var param2 = new SqlParameter("param1");

        Assert.Equal(param1.GetHashCode(), param2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ReturnsDifferentValue_ForNamedParametersWithDifferentNames()
    {
        var param1 = new SqlParameter("param1");
        var param2 = new SqlParameter("param2");

        Assert.NotEqual(param1.GetHashCode(), param2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ReturnsSameValue_ForIdenticalPositionalParameters()
    {
        var param1 = new SqlParameter("?");
        var param2 = new SqlParameter("?");

        Assert.Equal(param1.GetHashCode(), param2.GetHashCode());
    }
}
