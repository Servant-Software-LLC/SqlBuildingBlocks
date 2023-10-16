using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.QueryProcessing;

namespace SqlBuildingBlocks.Visitors;

/// <summary>
/// This visitor has the typical purpose of allowing one to replace <see cref="SqlFunction"/> entities with the result of their evaluation as 
/// a <see cref="SqlLiteralValue"/>.  Since a SQL query engine typically only evaluates scalar functions and informational functions only once
/// per execution and not for each row, this gives the consumer of the <see cref="QueryEngine"/> to make that evaluation before <see cref="QueryEngine.Query"/>
/// is called.  Column functions like UPPER(column) and aggregate functions that include a column reference must be evaluated within the query
/// engine, since they are calculated in the context of each row (or each grouping in the case of aggregates as it works in conjuction with 
/// GROUP BY clauses.
/// </summary>
public abstract class ResolveFunctionsVisitorBase : ISqlExpressionVisitor, ISqlValueVisitor
{
    public virtual void Visit(SqlBinaryExpression binExpr) { }
    public virtual SqlExpression? Visit(SqlColumnRef column) => null;
    public virtual SqlExpression? Visit(SqlParameter parameter) => null;
    public SqlExpression? Visit(SqlFunction sqlFunction) => VisitReturnExpression(sqlFunction);
    public virtual SqlExpression? Visit(SqlLiteralValue value) => null;

    SqlLiteralValue? ISqlValueVisitor.Visit(SqlParameter parameter) => null;

    SqlLiteralValue? ISqlValueVisitor.Visit(SqlFunction sqlFunction) => VisitReturnValue(sqlFunction);

    SqlLiteralValue? ISqlValueVisitor.Visit(SqlLiteralValue value) => null;

    SqlLimitValue? ISqlValueVisitor.Visit(SqlLimitValue limit) => null;

    protected abstract SqlExpression? VisitReturnExpression(SqlFunction sqlFunction);

    protected abstract SqlLiteralValue? VisitReturnValue(SqlFunction sqlFunction);

}
