namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents a SQL:2003 single-field interval literal (e.g., <c>INTERVAL '1' DAY</c>).
/// Used by <see cref="SqlWindowFrameBoundOffset"/> to model RANGE-mode window-frame bounds whose
/// offset is in the column's domain rather than a row count.
/// </summary>
public class IntervalLiteral
{
    /// <summary>
    /// Constructs an interval literal of the given magnitude and qualifier.
    /// </summary>
    /// <param name="magnitude">
    /// Number of <paramref name="qualifier"/> units. Must be non-negative; the PRECEDING /
    /// FOLLOWING <see cref="WindowFrameBoundType"/> on the bound conveys direction.
    /// </param>
    /// <param name="qualifier">The unit of the interval (DAY, HOUR, etc.).</param>
    public IntervalLiteral(long magnitude, IntervalQualifier qualifier)
    {
        if (magnitude < 0)
            throw new ArgumentOutOfRangeException(nameof(magnitude),
                "Interval magnitude must be non-negative; PRECEDING/FOLLOWING on the bound conveys direction.");

        Magnitude = magnitude;
        Qualifier = qualifier;
    }

    public long Magnitude { get; }
    public IntervalQualifier Qualifier { get; }

    public override string ToString() => $"INTERVAL '{Magnitude}' {Qualifier.ToString().ToUpperInvariant()}";
}

/// <summary>
/// SQL:2003 single-field interval qualifiers supported for window-frame bounds.
/// </summary>
public enum IntervalQualifier
{
    Year,
    Month,
    Day,
    Hour,
    Minute,
    Second
}
