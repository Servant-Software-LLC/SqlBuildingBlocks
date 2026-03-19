using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.Visitors;
using System.Data.Common;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlUpdateDefinition
{
    public SqlTable? Table { get; set;  }

    public SqlTable? SourceTable { get; set; }

    public IList<SqlJoin> Joins { get; set; } = new List<SqlJoin>();

    public IList<SqlAssignment> Assignments { get; private set; } = new List<SqlAssignment>();

    public SqlExpression? WhereClause { get; set; }

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
        where TVisitor : ISqlValueVisitor, ISqlExpressionVisitor
    {
        AcceptAssignments(vistor);
        AcceptJoins(vistor);

        AcceptWhereClause(vistor);
    }

    private void AcceptAssignments<TVisitor>(TVisitor visitor)
        where TVisitor : ISqlValueVisitor, ISqlExpressionVisitor
    {
        foreach (SqlAssignment assignment in Assignments)
        {
            assignment.Expression.Accept(visitor);
        }
    }

    public void AcceptWhereClause(ISqlExpressionVisitor sqlExpressionVisitor)
    {
        if (WhereClause != null)
        {
            WhereClause.Accept(sqlExpressionVisitor);
        }
    }

    public void AcceptJoins(ISqlExpressionVisitor sqlExpressionVisitor)
    {
        foreach (SqlJoin join in Joins)
        {
            join.Condition.Accept(sqlExpressionVisitor);
        }
    }


}
