using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Interfaces;

public interface ISqlExpressionVisitor
{
    /// <summary>
    /// Visits the <see cref="SqlBinaryExpression" in the <see cref="SqlExpression"/> tree./>
    /// </summary>
    /// <param name="binExpr"></param>
    void Visit(SqlBinaryExpression binExpr);

    /// <summary>
    /// Visits the <see cref="SqlColumnRef" in the <see cref="SqlExpression"/> tree./>
    /// </summary>
    /// <param name="column"></param>
    /// <returns>An expression which is the leaf node's replacement.  A <see cref="null"/> value implies no change.</returns>
    SqlExpression? Visit(SqlColumnRef column);

    /// <summary>
    /// Visits the <see cref="SqlParameter" in the <see cref="SqlExpression"/> tree./>
    /// </summary>
    /// <param name="column"></param>
    /// <returns>An expression which is the leaf node's replacement.  A <see cref="null"/> value implies no change.</returns>
    SqlExpression? Visit(SqlParameter parameter);

    /// <summary>
    /// Visits the <see cref="SqlFunction" in the <see cref="SqlExpression"/> tree./>
    /// </summary>
    /// <param name="column"></param>
    /// <returns>An expression which is the leaf node's replacement.  A <see cref="null"/> value implies no change.</returns>
    SqlExpression? Visit(SqlFunction function);

    /// <summary>
    /// Visits the <see cref="SqlLiteralValue" in the <see cref="SqlExpression"/> tree./>
    /// </summary>
    /// <param name="column"></param>
    /// <returns>An expression which is the leaf node's replacement.  A <see cref="null"/> value implies no change.</returns>
    SqlExpression? Visit(SqlLiteralValue value);
}
