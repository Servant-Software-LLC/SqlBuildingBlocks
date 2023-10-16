using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Visitors;

/// <summary>
/// Builds an <see cref="List{T}"/> of <see cref="SqlColumnRef"/> that are in the <see cref="SqlTable"/> provided in the ctor 
/// by walking the expression tree.
/// </summary>
class ColumnRefsVisitor : ISqlExpressionVisitor
{
    private readonly HashSet<SqlTable>? tables;
    private readonly bool includeTables;

    /// <summary>
    /// Ctor to include or exclude tables visited.
    /// </summary>
    /// <param name="tables">If null, then all tables are included.</param>
    /// <param name="includeTables">If tables is not null, then determines whether those tables are included or excluded.</param>
    public ColumnRefsVisitor(HashSet<SqlTable>? tables = null, bool includeTables = true)
    {
        this.tables = tables;
        this.includeTables = includeTables;
    }

    public List<SqlColumnRef> Results { get; } = new();

    public void Visit(SqlBinaryExpression binExpr) { }

    public SqlExpression? Visit(SqlColumnRef column)
    {
        var columnOfOperand = (SqlColumn)column.Column!;
        if (tables == null || 
           (includeTables && tables.Contains(columnOfOperand.TableRef!)) ||
           (!includeTables && !tables.Contains(columnOfOperand.TableRef!)))
                Results.Add(column);

        return null;
    }

    public SqlExpression? Visit(SqlParameter parameter) => null;

    public SqlExpression? Visit(SqlFunction function) => null;

    public SqlExpression? Visit(SqlLiteralValue value) => null;
}
