namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Discriminated-union offset attached to a <see cref="SqlWindowFrameBound"/> of type
/// <see cref="WindowFrameBoundType.Preceding"/> or <see cref="WindowFrameBoundType.Following"/>.
/// Mirrors the Wave 4 <c>SqlExpression</c> shape: exactly one arm is non-null at any time, and
/// <see cref="Kind"/> agrees with that arm. See <c>Docs/Architectural/window-frame-interval-decision.md</c>.
/// </summary>
public class SqlWindowFrameBoundOffset
{
    /// <summary>
    /// Constructs a numeric (row-count) offset for ROWS-mode frames.
    /// </summary>
    public SqlWindowFrameBoundOffset(int rowCount)
    {
        if (rowCount < 0)
            throw new ArgumentOutOfRangeException(nameof(rowCount),
                "Row-count offset must be non-negative; PRECEDING/FOLLOWING on the bound conveys direction.");

        RowCount = rowCount;
        Kind = SqlWindowFrameBoundOffsetKind.Numeric;
        AssertInvariant();
    }

    /// <summary>
    /// Constructs an INTERVAL offset for RANGE-mode frames.
    /// </summary>
    public SqlWindowFrameBoundOffset(IntervalLiteral interval)
    {
        Interval = interval ?? throw new ArgumentNullException(nameof(interval));
        Kind = SqlWindowFrameBoundOffsetKind.Interval;
        AssertInvariant();
    }

    /// <summary>
    /// Identifies which arm of this discriminated union is populated.
    /// </summary>
    public SqlWindowFrameBoundOffsetKind Kind { get; }

    /// <summary>
    /// The numeric row-count offset for ROWS-mode bounds. Set iff <see cref="Kind"/> is
    /// <see cref="SqlWindowFrameBoundOffsetKind.Numeric"/>.
    /// </summary>
    public int? RowCount { get; }

    /// <summary>
    /// The INTERVAL offset for RANGE-mode bounds. Set iff <see cref="Kind"/> is
    /// <see cref="SqlWindowFrameBoundOffsetKind.Interval"/>.
    /// </summary>
    public IntervalLiteral? Interval { get; }

    public override string ToString() => Kind switch
    {
        SqlWindowFrameBoundOffsetKind.Numeric => RowCount!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        SqlWindowFrameBoundOffsetKind.Interval => Interval!.ToString(),
        _ => throw new ArgumentOutOfRangeException()
    };

    /// <summary>
    /// Validates that exactly one arm is non-null and matches <see cref="Kind"/>.
    /// </summary>
    private void AssertInvariant()
    {
        var populated = new List<SqlWindowFrameBoundOffsetKind>();
        if (RowCount != null) populated.Add(SqlWindowFrameBoundOffsetKind.Numeric);
        if (Interval != null) populated.Add(SqlWindowFrameBoundOffsetKind.Interval);

        if (populated.Count == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(SqlWindowFrameBoundOffset)} invariant violated: no arm is populated. Expected exactly one arm matching {nameof(Kind)}={Kind}.");
        }

        if (populated.Count > 1)
        {
            throw new InvalidOperationException(
                $"{nameof(SqlWindowFrameBoundOffset)} invariant violated: {populated.Count} arms are populated ({string.Join(", ", populated)}). Exactly one arm must be non-null.");
        }

        if (populated[0] != Kind)
        {
            throw new InvalidOperationException(
                $"{nameof(SqlWindowFrameBoundOffset)} invariant violated: populated arm is {populated[0]} but {nameof(Kind)} is {Kind}. They must agree.");
        }
    }
}

/// <summary>
/// Identifies which arm of <see cref="SqlWindowFrameBoundOffset"/> is populated.
/// </summary>
public enum SqlWindowFrameBoundOffsetKind
{
    /// <summary>Row-count offset for ROWS-mode frames (e.g., <c>3 PRECEDING</c>).</summary>
    Numeric,
    /// <summary>INTERVAL offset for RANGE-mode frames (e.g., <c>INTERVAL '1' DAY PRECEDING</c>).</summary>
    Interval
}
