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
            throw new ArgumentNullException(nameof(columnOfOperand.TableRef), $"{nameof(columnOfOperand)}.{nameof(columnOfOperand.TableRef)} cannot be null.");

        if (!tables.Contains(columnOfOperand.TableRef))
            Result = false;

        return null;
    }

    public SqlExpression? Visit(SqlParameter parameter) => null;

    // TODO: In the future, will we need to investigate the function arguments for the tables of any columns specified?
    public SqlExpression? Visit(SqlFunction function) => null;

    public SqlExpression? Visit(SqlLiteralValue value) => null;

    public void Visit(SqlCastExpression castExpr) { }

    public void Visit(SqlArrayConstructor arrayConstructor) { }

    public void Visit(SqlArraySubscript arraySubscript) { }

    public void Visit(SqlJsonExpression jsonExpr) { }
}
