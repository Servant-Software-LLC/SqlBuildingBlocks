using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.Visitors;
using System.Data.Common;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlInsertDefinition
{
    public SqlTable? Table { get; set; }

    public IList<SqlColumn> Columns { get; } = new List<SqlColumn>();


    //Either Values or SelectDefinition will be set, but not both properties.

    public IList<SqlExpression>? Values { get; set; }

    public SqlSelectDefinition? SelectDefinition { get; set; }

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
    }

    public void AcceptColumns(ISqlExpressionVisitor visitor)
    {
        foreach (var value in Values!)
        {
            value.Accept(visitor);
        }
    }

}
