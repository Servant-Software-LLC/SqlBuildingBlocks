using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.Utils;
using SqlBuildingBlocks.Visitors;
using System.Data.Common;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlSelectDefinition
{
    public IList<ISqlColumn> Columns { get; private set; } = new List<ISqlColumn>();

    public SqlTable? Table { get; set; }

    public IList<SqlJoin> Joins { get; set; } = new List<SqlJoin>();

    public SqlBinaryExpression? WhereClause { get; set; }

    //TODO: Maltby - Need ORDER BY columns

    public SqlLimitOffset? Limit { get; set; }

    public string? InvalidReferenceReason { get; set; }
    public bool InvalidReferences => !string.IsNullOrEmpty(InvalidReferenceReason);

    public void ResolveReferences(IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider, IFunctionProvider? functionProvider = null)
    {
        //Resolve for all tables the database that they belong to.
        SelectReferenceResolver referenceResolver = new(this, databaseConnectionProvider, tableSchemaProvider, functionProvider);

        //Resolve the database of the tables.
        referenceResolver.ResolveTablesDatabase();

        //Resolve references.
        if (!InvalidReferences)
            referenceResolver.ResolveReferences();
    }

    public void ResolveParameters(DbParameterCollection parameters) =>
        Accept(new ResolveParametersVisitor(parameters));

    public void ResolveParameters(Dictionary<SqlParameter, SqlLiteralValue> parameters) =>
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
        AcceptColumns(vistor);

        AcceptBinaryExpressions(vistor);
    }


    public IList<SqlTable> TablesInSelect
    {
        get
        {
            if (Table is null)
                return new List<SqlTable>();

            List<SqlTable> tablesInSelect = new() { Table };

            if (Joins != null && Joins.Count > 0)
            {
                tablesInSelect.AddRange(Joins.Select(join => join.Table));
            }

            return tablesInSelect;
        }
    }

    public void AcceptColumns(ISqlValueVisitor visitor) 
    {
        List<ISqlColumn> resolvedColumns = new();
        int unnamedColumnIndex = 1;
        foreach (ISqlColumn column in Columns)
        {
            if (column is ISqlColumnWithAlias columnWithAlias) 
            {
                SqlLiteralValue? sqlLiteralValue = null;
                switch (column)
                {
                    case SqlParameterColumn sqlParameterColumn:
                        sqlLiteralValue = visitor.Visit(sqlParameterColumn.Parameter);
                        break;

                    case SqlFunctionColumn functionColumn:
                        sqlLiteralValue = visitor.Visit(functionColumn.Function);
                        break;

                    case SqlLiteralValueColumn literalValueColumn:
                        sqlLiteralValue = visitor.Visit(literalValueColumn.Value);
                        break;
                }

                if (sqlLiteralValue != null)
                {
                    var columnAlias = string.IsNullOrEmpty(columnWithAlias.ColumnAlias) ? 
                                        $"Column{unnamedColumnIndex++}" : 
                                        columnWithAlias.ColumnAlias;

                    resolvedColumns.Add(new SqlLiteralValueColumn(sqlLiteralValue) { ColumnAlias = columnAlias });
                    continue;
                }
            }

            resolvedColumns.Add(column);
        }

        Columns = resolvedColumns;
    }

    public void AcceptBinaryExpressions(ISqlExpressionVisitor sqlExpressionVisitor)
    {
        AcceptJoins(sqlExpressionVisitor);
        AcceptWhereClause(sqlExpressionVisitor);
    }

    public void AcceptJoins(ISqlExpressionVisitor sqlExpressionVisitor)
    {
        foreach (SqlJoin join in Joins)
        {
            join.Condition.Accept(sqlExpressionVisitor);
        }
    }

    public void AcceptWhereClause(ISqlExpressionVisitor sqlExpressionVisitor) => WhereClause?.Accept(sqlExpressionVisitor);

    public void AccessLimitOffset(ISqlValueVisitor sqlValueVisitor) => Limit?.Accept(sqlValueVisitor);
}
