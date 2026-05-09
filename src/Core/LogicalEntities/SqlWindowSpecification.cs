namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents the window specification in an OVER clause.
/// e.g. OVER (PARTITION BY dept ORDER BY salary DESC ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)
/// </summary>
public class SqlWindowSpecification
{
    /// <summary>
    /// The expressions in the PARTITION BY clause, or empty if no PARTITION BY.
    /// </summary>
    public IList<SqlExpression> PartitionBy { get; set; } = new List<SqlExpression>();

    /// <summary>
    /// The columns in the ORDER BY clause within the window, or empty if no ORDER BY.
    /// </summary>
    public IList<SqlOrderByColumn> OrderBy { get; set; } = new List<SqlOrderByColumn>();

    /// <summary>
    /// The optional frame clause (ROWS/RANGE BETWEEN ... AND ...).
    /// </summary>
    public SqlWindowFrame? Frame { get; set; }

    public override string ToString()
    {
        var parts = new List<string>();

        if (PartitionBy.Count > 0)
            parts.Add("PARTITION BY " + string.Join(", ", PartitionBy.Select(p => p.ToString())));

        if (OrderBy.Count > 0)
            parts.Add("ORDER BY " + string.Join(", ", OrderBy.Select(o =>
                o.Descending ? $"{o.ColumnName} DESC" : o.ColumnName)));

        if (Frame != null)
            parts.Add(Frame.ToString());

        return "OVER (" + string.Join(" ", parts) + ")";
    }
}

/// <summary>
/// Represents a window frame clause: ROWS/RANGE/GROUPS BETWEEN start AND end.
/// </summary>
public class SqlWindowFrame
{
    public SqlWindowFrame(WindowFrameMode mode, SqlWindowFrameBound start, SqlWindowFrameBound? end = null)
    {
        Mode = mode;
        Start = start ?? throw new ArgumentNullException(nameof(start));
        End = end;
    }

    public WindowFrameMode Mode { get; }
    public SqlWindowFrameBound Start { get; }
    public SqlWindowFrameBound? End { get; }

    public override string ToString()
    {
        var modeStr = Mode switch
        {
            WindowFrameMode.Rows => "ROWS",
            WindowFrameMode.Range => "RANGE",
            WindowFrameMode.Groups => "GROUPS",
            _ => throw new ArgumentOutOfRangeException()
        };

        if (End != null)
            return $"{modeStr} BETWEEN {Start} AND {End}";

        return $"{modeStr} {Start}";
    }
}

/// <summary>
/// Represents a single bound in a window frame clause.
/// </summary>
public class SqlWindowFrameBound
{
    /// <summary>
    /// Constructs a window frame bound. For PRECEDING/FOLLOWING bounds, supply a numeric offset
    /// (interpreted as a row count for ROWS-mode frames). For RANGE-mode INTERVAL bounds, use the
    /// <see cref="SqlWindowFrameBound(WindowFrameBoundType, IntervalLiteral)"/> overload.
    /// For UNBOUNDED PRECEDING / UNBOUNDED FOLLOWING / CURRENT ROW, omit the offset.
    /// </summary>
    public SqlWindowFrameBound(WindowFrameBoundType type, int? offset = null)
    {
        Type = type;
        Offset = offset is null ? null : new SqlWindowFrameBoundOffset(offset.Value);
    }

    /// <summary>
    /// Constructs a window frame bound with an INTERVAL offset (e.g., <c>INTERVAL '1' DAY PRECEDING</c>).
    /// Valid only with <see cref="WindowFrameBoundType.Preceding"/> or <see cref="WindowFrameBoundType.Following"/>.
    /// </summary>
    public SqlWindowFrameBound(WindowFrameBoundType type, IntervalLiteral interval)
    {
        if (interval is null) throw new ArgumentNullException(nameof(interval));
        if (type != WindowFrameBoundType.Preceding && type != WindowFrameBoundType.Following)
            throw new ArgumentException(
                $"INTERVAL offsets are only valid on {nameof(WindowFrameBoundType.Preceding)}/{nameof(WindowFrameBoundType.Following)} bounds.",
                nameof(type));

        Type = type;
        Offset = new SqlWindowFrameBoundOffset(interval);
    }

    public WindowFrameBoundType Type { get; }

    /// <summary>
    /// The offset for PRECEDING/FOLLOWING bounds. Null for UNBOUNDED and CURRENT ROW. The offset
    /// is itself a discriminated union — numeric (row count) or INTERVAL — see
    /// <see cref="SqlWindowFrameBoundOffset"/>.
    /// </summary>
    public SqlWindowFrameBoundOffset? Offset { get; }

    public override string ToString() => Type switch
    {
        WindowFrameBoundType.UnboundedPreceding => "UNBOUNDED PRECEDING",
        WindowFrameBoundType.Preceding => $"{Offset} PRECEDING",
        WindowFrameBoundType.CurrentRow => "CURRENT ROW",
        WindowFrameBoundType.Following => $"{Offset} FOLLOWING",
        WindowFrameBoundType.UnboundedFollowing => "UNBOUNDED FOLLOWING",
        _ => throw new ArgumentOutOfRangeException()
    };
}

public enum WindowFrameMode
{
    Rows,
    Range,
    Groups
}

public enum WindowFrameBoundType
{
    UnboundedPreceding,
    Preceding,
    CurrentRow,
    Following,
    UnboundedFollowing
}

/// <summary>
/// Identifies well-known named window functions.
/// </summary>
public enum WindowFunctionType
{
    /// <summary>Not a recognized named window function.</summary>
    None,
    /// <summary>ROW_NUMBER() — sequential row number within the partition.</summary>
    RowNumber,
    /// <summary>RANK() — rank with gaps for ties.</summary>
    Rank,
    /// <summary>DENSE_RANK() — rank without gaps for ties.</summary>
    DenseRank,
    /// <summary>NTILE(n) — distributes rows into n buckets.</summary>
    Ntile,
    /// <summary>LAG(expr [, offset [, default]]) — access a previous row's value.</summary>
    Lag,
    /// <summary>LEAD(expr [, offset [, default]]) — access a following row's value.</summary>
    Lead,
    /// <summary>FIRST_VALUE(expr) — first value in the window frame.</summary>
    FirstValue,
    /// <summary>LAST_VALUE(expr) — last value in the window frame.</summary>
    LastValue,
    /// <summary>NTH_VALUE(expr, n) — nth value in the window frame.</summary>
    NthValue
}
