using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.Utils;

internal class DetectVisitedVisitor : ISqlExpressionVisitor
{
    public bool VisitedBinaryExpression { get; private set; }
    public bool VisitedColumnRef { get; private set; }
    public bool VisitedParameter { get; private set; }
    public bool VisitedFunction { get; private set; }
    public bool VisitedValue { get; private set; }

    public void Visit(SqlBinaryExpression binExpr)
    {
        Assert.NotNull(binExpr);
        VisitedBinaryExpression = true;
    }

    SqlExpression ISqlExpressionVisitor.Visit(SqlColumnRef column)
    {
        Assert.NotNull(column);
        VisitedColumnRef = true;
        return null;
    }

    SqlExpression ISqlExpressionVisitor.Visit(SqlParameter parameter)
    {
        Assert.NotNull(parameter);
        VisitedParameter = true;
        return null;
    }

    SqlExpression ISqlExpressionVisitor.Visit(SqlFunction function)
    {
        Assert.NotNull(function);
        VisitedFunction = true;
        return null;
    }

    SqlExpression ISqlExpressionVisitor.Visit(SqlLiteralValue value)
    {
        Assert.NotNull(value);
        VisitedValue = true;
        return null;
    }
}
