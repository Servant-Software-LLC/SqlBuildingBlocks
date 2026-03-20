using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;


namespace SqlBuildingBlocks.Core.Tests.Utils;

internal class DetectVisitedVisitor : ISqlExpressionVisitor
{
    public bool VisitedBinaryExpression { get; private set; }
    public bool VisitedBetweenExpression { get; private set; }
    public bool VisitedCaseExpression { get; private set; }
    public bool VisitedExistsExpression { get; private set; }
    public bool VisitedScalarSubqueryExpression { get; private set; }
    public bool VisitedInList { get; private set; }
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

    public void Visit(SqlExistsExpression existsExpr)
    {
        Assert.NotNull(existsExpr);
        VisitedExistsExpression = true;
    }

    public void Visit(SqlScalarSubqueryExpression scalarSubqueryExpr)
    {
        Assert.NotNull(scalarSubqueryExpr);
        VisitedScalarSubqueryExpression = true;
    }
    
    public void Visit(SqlInList inList)
    {
        Assert.NotNull(inList);
        VisitedInList = true;
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

    public void Visit(SqlCastExpression castExpr) { }

    public void Visit(SqlArrayConstructor arrayConstructor) { }

    public void Visit(SqlArraySubscript arraySubscript) { }
}
