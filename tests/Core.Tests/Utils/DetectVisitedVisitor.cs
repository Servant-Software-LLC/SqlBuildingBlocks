using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.Utils;

internal class DetectVisitedVisitor : ISqlExpressionVisitor
{
    public bool VisitedBinaryExpression { get; private set; }
    public bool VisitedBetweenExpression { get; private set; }
    public bool VisitedCaseExpression { get; private set; }
    public bool VisitedColumnRef { get; private set; }
    public bool VisitedParameter { get; private set; }
    public bool VisitedFunction { get; private set; }
    public bool VisitedValue { get; private set; }

    public void Visit(SqlBinaryExpression binExpr)
    {
        Assert.NotNull(binExpr);
        VisitedBinaryExpression = true;
    }

    public void Visit(SqlBetweenExpression betweenExpr)
    {
        Assert.NotNull(betweenExpr);
        VisitedBetweenExpression = true;
    }

    public void Visit(SqlCaseExpression caseExpr)
    {
        Assert.NotNull(caseExpr);
        VisitedCaseExpression = true;
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
