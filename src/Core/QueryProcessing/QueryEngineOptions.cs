namespace SqlBuildingBlocks.QueryProcessing;

/// <summary>
/// Tunable execution-time options for <see cref="QueryEngine"/>.
/// </summary>
/// <remarks>
/// Introduced for issue #168 (recursive CTE execution). Kept minimal — one knob —
/// per the recursive-CTE-semantics ADR. Extend additively (one property per knob)
/// rather than re-shaping this type, so existing consumers continue to compile
/// against the parameterless default.
/// </remarks>
public sealed class QueryEngineOptions
{
    private int maxRecursionDepth = 100;

    /// <summary>
    /// Maximum number of iterations the recursive-CTE executor will perform before
    /// raising <see cref="Exceptions.SqlExecutionException"/>. Matches SQL Server's
    /// default <c>MAXRECURSION</c> of 100. Must be at least 1.
    /// </summary>
    /// <remarks>
    /// There is no sentinel value for "no limit": consumers who need effectively
    /// unlimited recursion should set a large finite value (e.g. <see cref="int.MaxValue"/>).
    /// </remarks>
    public int MaxRecursionDepth
    {
        get => maxRecursionDepth;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"{nameof(MaxRecursionDepth)} must be at least 1.");
            }

            maxRecursionDepth = value;
        }
    }
}
