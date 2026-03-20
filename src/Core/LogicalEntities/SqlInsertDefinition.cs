using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.Visitors;
using System.Data.Common;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlInsertDefinition
{
    public SqlTable? Table { get; set; }

    public IList<SqlColumn> Columns { get; } = new List<SqlColumn>();


    //Either Values or SelectDefinition will be set, but not both properties.

    /// <summary>
    /// Each element is one row of VALUES expressions.
    /// For example, VALUES (1,2),(3,4) produces two inner lists of two expressions each.
    /// </summary>
    public IList<IList<SqlExpression>>? Values { get; set; }

    public SqlSelectDefinition? SelectDefinition { get; set; }

    /// <summary>
    /// Optional upsert clause for conflict handling.
    /// PostgreSQL: ON CONFLICT (columns) DO UPDATE SET ... / DO NOTHING
    /// MySQL: ON DUPLICATE KEY UPDATE ...
    /// </summary>
    public SqlUpsertClause? UpsertClause { get; set; }

    /// <summary>
    /// SQL Server OUTPUT clause for capturing inserted rows.
    /// </summary>
    public SqlOutputClause? OutputClause { get; set; }

    /// <summary>
    /// PostgreSQL RETURNING clause for capturing inserted rows.
    /// </summary>
    public SqlReturningClause? ReturningClause { get; set; }

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
        //Since the ParameterToValueConverter is using named parameters, we can reuse the vistor multiple times.
        if (Values != null)
            AcceptColumns(vistor);
        else
            SelectDefinition!.Accept(vistor);

        AcceptUpsertClause(vistor);
    }

    public void AcceptColumns(ISqlExpressionVisitor visitor)
    {
        foreach (var row in Values!)
        {
            foreach (var value in row)
            {
                value.Accept(visitor);
            }
        }
    }

    private void AcceptUpsertClause(ISqlExpressionVisitor visitor)
    {
        if (UpsertClause == null)
            return;

        UpsertClause.ConflictTargetWhereCondition?.Accept(visitor);

        foreach (var assignment in UpsertClause.Assignments)
        {
            assignment.Expression.Accept(visitor);
        }

        UpsertClause.WhereCondition?.Accept(visitor);
    }

}
