using SqlBuildingBlocks.Interfaces;
using System.Data;
using System.Linq.Expressions;
using System.Text;

namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents a searched CASE expression: CASE WHEN c1 THEN r1 [WHEN c2 THEN r2 ...] [ELSE e] END.
/// </summary>
public class SqlCaseExpression
{
    public SqlCaseExpression(
        IReadOnlyList<(SqlExpression Condition, SqlExpression Result)> whenClauses,
        SqlExpression? elseResult)
    {
        WhenClauses = whenClauses ?? throw new ArgumentNullException(nameof(whenClauses));
        ElseResult = elseResult;
    }

    /// <summary>Ordered list of WHEN condition / THEN result pairs.</summary>
    public IReadOnlyList<(SqlExpression Condition, SqlExpression Result)> WhenClauses { get; }

    /// <summary>The ELSE expression, or <c>null</c> if no ELSE clause is present.</summary>
    public SqlExpression? ElseResult { get; }

    public Expression GetExpression(Dictionary<SqlTable, DataRow> substituteValues, SqlTable tableDataRow, ParameterExpression param)
    {
        // Build nested ternary: Condition(c1, r1, Condition(c2, r2, ... else))
        // Work backwards from the ELSE (or default) to build the chain.

        // A dummy companion used for type inference when sub-expressions need one.
        var dummyCompanion = new SqlExpression(new SqlLiteralValue());

        // Start with the ELSE branch (or a default value).
        Expression elseExpr;
        if (ElseResult != null)
        {
            elseExpr = ElseResult.GetExpression(substituteValues, tableDataRow, param, dummyCompanion);
        }
        else
        {
            // No ELSE clause — default to null (as object).
            elseExpr = Expression.Constant(null, typeof(object));
        }

        // Build the chain from the last WHEN clause backwards.
        for (int i = WhenClauses.Count - 1; i >= 0; i--)
        {
            var (condition, result) = WhenClauses[i];

            var condExpr = condition.GetExpression(substituteValues, tableDataRow, param, dummyCompanion);
            var resultExpr = result.GetExpression(substituteValues, tableDataRow, param, dummyCompanion);

            // Align result and else branch types so Expression.Condition succeeds.
            AlignTypes(ref resultExpr, ref elseExpr);

            elseExpr = Expression.Condition(condExpr, resultExpr, elseExpr);
        }

        return elseExpr;
    }

    private static void AlignTypes(ref Expression left, ref Expression right)
    {
        if (left.Type == right.Type)
            return;

        if (left.Type == typeof(object))
            left = Expression.Convert(left, right.Type);
        else if (right.Type == typeof(object))
            right = Expression.Convert(right, left.Type);
        else
        {
            try
            {
                right = Expression.Convert(right, left.Type);
            }
            catch
            {
                left = Expression.Convert(left, right.Type);
            }
        }
    }

    public void Accept(ISqlExpressionVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var (condition, result) in WhenClauses)
        {
            condition.Accept(visitor);
            result.Accept(visitor);
        }
        ElseResult?.Accept(visitor);
    }

    public string ToExpressionString()
    {
        var sb = new StringBuilder("CASE");
        foreach (var (condition, result) in WhenClauses)
            sb.Append($" WHEN {condition.ToExpressionString()} THEN {result.ToExpressionString()}");
        if (ElseResult != null)
            sb.Append($" ELSE {ElseResult.ToExpressionString()}");
        sb.Append(" END");
        return sb.ToString();
    }

    public override string ToString() => ToExpressionString();
}
