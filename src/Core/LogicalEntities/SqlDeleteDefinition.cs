using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.Visitors;
using System.Data.Common;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlDeleteDefinition
{
    public SqlTable? Table { get; set; }

    public SqlBinaryExpression? WhereClause { get; set; }
    public SqlReturning? Returning { get; set; }

    public void ResolveParameters(DbParameterCollection parameters) =>
        Accept(new ResolveParametersVisitor(parameters));

    /// <summary>
    /// Resolve functions which don't depend on the state of individual rows (for instance, LAST_INSERT_ID() depends on the previous SQL INSERT statement to determine its value whereas UPPER() would depend on the current row that is being evaluated.)
    /// </summary>
    /// <param name="functionProvider"></param>
    public void ResolveFunctions(IFunctionProvider functionProvider) =>
        Accept(new ResolveFunctionsVisitor(functionProvider));

    public void Accept<TVisitor>(TVisitor vistor)
        where TVisitor : ISqlExpressionVisitor
    {
        AcceptWhereClause(vistor);
    }

    public void AcceptWhereClause(ISqlExpressionVisitor sqlExpressionVisitor)
    {
        if (WhereClause != null)
        {
            WhereClause.Accept(sqlExpressionVisitor);
        }
    }

}
