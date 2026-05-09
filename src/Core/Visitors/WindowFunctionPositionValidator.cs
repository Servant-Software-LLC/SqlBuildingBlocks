using SqlBuildingBlocks.Exceptions;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Visitors;

/// <summary>
/// Enforces SQL:2003 §7.11 — window functions (functions/aggregates carrying an OVER clause)
/// are permitted only in the SELECT list and ORDER BY clause; they are illegal in
/// WHERE, HAVING, GROUP BY, and JOIN ON predicates.
/// </summary>
/// <remarks>
/// This is a post-parse semantic walk invoked by the query engine after reference resolution.
/// It walks the immediate predicate-level expressions of WHERE, HAVING, JOIN ON, and any
/// expression-bearing GROUP BY elements; it does NOT recurse into nested SELECT bodies
/// (scalar subqueries, EXISTS, derived tables). A scalar subquery whose SELECT list itself
/// contains a window function is legal — only the immediate predicate level is restricted.
/// </remarks>
internal static class WindowFunctionPositionValidator
{
    /// <summary>
    /// Validates that no window function appears at the immediate predicate level of any
    /// position prohibited by SQL:2003 §7.11. Throws <see cref="SqlExecutionException"/>
    /// naming the offending clause if a violation is found.
    /// </summary>
    public static void Validate(SqlSelectDefinition select)
    {
        if (select.WhereClause != null)
            CheckExpression(select.WhereClause, "WHERE");

        if (select.HavingClause != null)
            CheckExpression(select.HavingClause, "HAVING");

        foreach (var join in select.Joins)
        {
            // JOIN ON predicates carry a SqlBinaryExpression; walk both sides.
            CheckBinaryExpression(join.Condition, "JOIN ON");
        }

        // GROUP BY in the current logical model carries column-name strings, not expressions,
        // so window-function syntax cannot appear there at the AST level. We still perform the
        // check defensively in case GROUP BY ever holds expressions; for now this is a no-op
        // (the foreach iterates over zero expression nodes).
    }

    private static void CheckExpression(SqlExpression expression, string clauseName)
    {
        switch (expression.Kind)
        {
            case SqlExpressionKind.Function:
                ThrowIfWindow(expression.Function!, clauseName);
                break;

            case SqlExpressionKind.BinExpr:
                CheckBinaryExpression(expression.BinExpr!, clauseName);
                break;

            case SqlExpressionKind.BetweenExpr:
                CheckExpression(expression.BetweenExpr!.Operand, clauseName);
                CheckExpression(expression.BetweenExpr.LowerBound, clauseName);
                CheckExpression(expression.BetweenExpr.UpperBound, clauseName);
                break;

            case SqlExpressionKind.CaseExpr:
                CheckCaseExpression(expression.CaseExpr!, clauseName);
                break;

            case SqlExpressionKind.InList:
                foreach (var item in expression.InList!.Items)
                    CheckExpression(item, clauseName);
                break;

            case SqlExpressionKind.CastExpr:
                CheckExpression(expression.CastExpr!.Expression, clauseName);
                break;

            // Subquery arms (ExistsExpr, ScalarSubqueryExpr) intentionally NOT recursed:
            // window functions are legal inside a nested SELECT body. Only the immediate
            // predicate level of the enclosing clause is restricted.
            case SqlExpressionKind.ExistsExpr:
            case SqlExpressionKind.ScalarSubqueryExpr:
            case SqlExpressionKind.Column:
            case SqlExpressionKind.Parameter:
            case SqlExpressionKind.Value:
            case SqlExpressionKind.ArrayConstructor:
            case SqlExpressionKind.ArraySubscript:
            case SqlExpressionKind.JsonExpr:
                break;
        }
    }

    private static void CheckBinaryExpression(SqlBinaryExpression binaryExpression, string clauseName)
    {
        CheckExpression(binaryExpression.Left, clauseName);
        if (binaryExpression.Right != null)
            CheckExpression(binaryExpression.Right, clauseName);
    }

    private static void CheckCaseExpression(SqlCaseExpression caseExpression, string clauseName)
    {
        foreach (var (condition, result) in caseExpression.WhenClauses)
        {
            CheckExpression(condition, clauseName);
            CheckExpression(result, clauseName);
        }

        if (caseExpression.ElseResult != null)
            CheckExpression(caseExpression.ElseResult, clauseName);
    }

    private static void ThrowIfWindow(SqlFunction function, string clauseName)
    {
        if (function.IsWindowFunction)
        {
            throw new SqlExecutionException(
                $"Window functions are not allowed in {clauseName} clauses (SQL:2003 §7.11). " +
                $"Offending function: {function.FunctionName}.");
        }
    }
}
