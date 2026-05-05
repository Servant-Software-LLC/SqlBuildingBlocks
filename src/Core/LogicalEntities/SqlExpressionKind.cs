namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Identifies which arm of the <see cref="SqlExpression"/> discriminated union is populated.
/// Exactly one arm of a <see cref="SqlExpression"/> is non-null at any time, and that arm is
/// reported by <see cref="SqlExpression.Kind"/>.
/// </summary>
/// <remarks>
/// Names mirror the property names on <see cref="SqlExpression"/> for easy correspondence:
/// <c>Column</c> → <see cref="SqlExpression.Column"/>, <c>Parameter</c> → <see cref="SqlExpression.Parameter"/>,
/// and so on. When a new arm is added to <see cref="SqlExpression"/>, a matching value MUST be added
/// here, a constructor must set <see cref="SqlExpression.Kind"/>, and the
/// <c>GetExpression</c>/<c>ToExpressionString</c>/<c>ToString</c>/<c>Accept</c> switches must be extended.
/// </remarks>
public enum SqlExpressionKind
{
    Column,
    Parameter,
    Function,
    Value,
    BinExpr,
    BetweenExpr,
    CaseExpr,
    ExistsExpr,
    ScalarSubqueryExpr,
    InList,
    CastExpr,
    ArrayConstructor,
    ArraySubscript,
    JsonExpr,
}
