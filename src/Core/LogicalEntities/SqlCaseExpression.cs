using SqlBuildingBlocks.Interfaces;
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
