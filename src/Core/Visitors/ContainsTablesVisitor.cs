using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Visitors;

class ContainsTablesVisitor : ISqlExpressionVisitor
{
    private readonly HashSet<SqlTable> tables;

    public ContainsTablesVisitor(HashSet<SqlTable> tables)
    {
        this.tables = tables ?? throw new ArgumentNullException(nameof(tables));
    }

    /// <summary>
    /// Indicates if only <see cref="SqlTable"/> instances provided in the HashSet parameter of the ctor were encountered.
    /// </summary>
    public bool Result { get; private set; } = true;

    public void Visit(SqlBinaryExpression binExpr) { }

    public void Visit(SqlBetweenExpression betweenExpr) { }

    public void Visit(SqlCaseExpression caseExpr) { }

    public void Visit(SqlExistsExpression existsExpr) { }

    public void Visit(SqlScalarSubqueryExpression scalarSubqueryExpr) { }
    
    public void Visit(SqlInList inList) { }

    public SqlExpression? Visit(SqlColumnRef column)
    {
        if (column.Column is not SqlColumn columnOfOperand)
            throw new ArgumentException($"The {nameof(column)}.{nameof(column.Column)} parameter must be convertable to a {typeof(SqlColumnRef)}.", nameof(column));

        if (columnOfOperand.TableRef is null)
        {
            // Column without a table reference cannot be verified — treat as not contained.
            Result = false;
            return null;
        }

        if (!tables.Contains(columnOfOperand.TableRef))
            Result = false;

        return null;
    }

    public SqlExpression? Visit(SqlParameter parameter) => null;

    public SqlExpression? Visit(SqlFunction function)
    {
        // Walk into function arguments to check table references of any columns specified.
        foreach (var arg in function.Arguments)
            arg.Accept(this);
        return null;
    }

    public SqlExpression? Visit(SqlLiteralValue value) => null;

    public void Visit(SqlCastExpression castExpr) { }

    public void Visit(SqlArrayConstructor arrayConstructor) { }

    public void Visit(SqlArraySubscript arraySubscript) { }

    public void Visit(SqlJsonExpression jsonExpr) { }
}
