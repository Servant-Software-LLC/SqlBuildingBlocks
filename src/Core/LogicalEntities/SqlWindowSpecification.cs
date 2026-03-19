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
    public SqlWindowFrameBound(WindowFrameBoundType type, int? offset = null)
    {
        Type = type;
        Offset = offset;
    }

    public WindowFrameBoundType Type { get; }

    /// <summary>
    /// The numeric offset for PRECEDING/FOLLOWING bounds. Null for UNBOUNDED and CURRENT ROW.
    /// </summary>
    public int? Offset { get; }

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
