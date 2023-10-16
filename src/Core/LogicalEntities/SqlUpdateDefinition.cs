using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.Visitors;
using System.Data.Common;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlUpdateDefinition
{
    public SqlTable Table { get; set;  }

    public IList<SqlAssignment> Assignments { get; private set; } = new List<SqlAssignment>();

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
        where TVisitor : ISqlValueVisitor, ISqlExpressionVisitor
    {
        AcceptAssignments(vistor);

        AcceptWhereClause(vistor);
    }

    private void AcceptAssignments(ISqlValueVisitor sqlValueVisitor)
    {
        List<SqlAssignment> resolvedAssignments = new();
        foreach (SqlAssignment assignment in Assignments)
        {
            SqlLiteralValue? sqlLiteralValue = assignment.Parameter != null ? sqlValueVisitor.Visit(assignment.Parameter!) :
                assignment.Function != null ? sqlValueVisitor.Visit(assignment.Function!) :
                sqlValueVisitor.Visit(assignment.Value!);

            var resolvedAssignment = sqlLiteralValue != null ?
                new SqlAssignment(assignment.Column, sqlLiteralValue) :
                assignment;

            resolvedAssignments.Add(resolvedAssignment);
        }

        Assignments = resolvedAssignments;
    }

    public void AcceptWhereClause(ISqlExpressionVisitor sqlExpressionVisitor)
    {
        if (WhereClause != null)
        {
            WhereClause.Accept(sqlExpressionVisitor);
        }
    }


}
