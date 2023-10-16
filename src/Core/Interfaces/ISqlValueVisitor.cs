using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Interfaces;

public interface ISqlValueVisitor
{
    /// <summary>
    /// Visits the <see cref="SqlParameter" in the walk of SQL statements AST./>
    /// </summary>
    /// <param name="column"></param>
    /// <returns>An expression which is the leaf node's replacement.  A <see cref="null"/> value implies no change.</returns>
    SqlLiteralValue? Visit(SqlParameter parameter);

    /// <summary>
    /// Visits the <see cref="SqlFunction" in the walk of SQL statements AST./>
    /// </summary>
    /// <param name="function"></param>
    /// <returns>An expression which is the leaf node's replacement.  A <see cref="null"/> value implies no change.</returns>
    SqlLiteralValue? Visit(SqlFunction function);

    /// <summary>
    /// Visits the <see cref="SqlLiteralValue" in the walk of SQL statements AST./>
    /// </summary>
    /// <param name="value"></param>
    /// <returns>An expression which is the leaf node's replacement.  A <see cref="null"/> value implies no change.</returns>
    SqlLiteralValue? Visit(SqlLiteralValue value);

    /// <summary>
    /// Visits the <see cref="SqlLimitValue" in the walk of SQL statements AST./>
    /// </summary>
    /// <param name="limit"></param>
    /// <returns>An expression which is the leaf node's replacement.  A <see cref="null"/> value implies no change.</returns>
    SqlLimitValue? Visit(SqlLimitValue limit);
}
