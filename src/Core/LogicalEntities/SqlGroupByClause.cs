namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents a GROUP BY clause, including optional ROLLUP, CUBE, or GROUPING SETS modifiers.
/// </summary>
public class SqlGroupByClause
{
    /// <summary>
    /// The simple column-based grouping elements (e.g. GROUP BY col1, col2).
    /// </summary>
    public IList<string> Columns { get; set; } = new List<string>();

    /// <summary>
    /// The grouping set specifications (ROLLUP, CUBE, GROUPING SETS).
    /// </summary>
    public IList<SqlGroupingSet> GroupingSets { get; set; } = new List<SqlGroupingSet>();

    public override string ToString()
    {
        var parts = new List<string>();

        foreach (var col in Columns)
            parts.Add(col);

        foreach (var gs in GroupingSets)
            parts.Add(gs.ToString());

        return "GROUP BY " + string.Join(", ", parts);
    }
}

/// <summary>
/// Represents a ROLLUP, CUBE, or GROUPING SETS specification within a GROUP BY clause.
/// </summary>
public class SqlGroupingSet
{
    public SqlGroupingSet(GroupingSetType type)
    {
        Type = type;
    }

    public GroupingSetType Type { get; }

    /// <summary>
    /// For ROLLUP and CUBE: the list of column lists (each element is a list for composite columns).
    /// For GROUPING SETS: each element is a grouping set (a list of columns; empty list represents ()).
    /// </summary>
    public IList<IList<string>> Sets { get; set; } = new List<IList<string>>();

    public override string ToString()
    {
        var keyword = Type switch
        {
            GroupingSetType.Rollup => "ROLLUP",
            GroupingSetType.Cube => "CUBE",
            GroupingSetType.GroupingSets => "GROUPING SETS",
            _ => throw new ArgumentOutOfRangeException()
        };

        if (Type == GroupingSetType.GroupingSets)
        {
            var setStrings = Sets.Select(s => s.Count == 0 ? "()" : "(" + string.Join(", ", s) + ")");
            return $"{keyword}({string.Join(", ", setStrings)})";
        }

        // ROLLUP and CUBE: simple comma-separated columns
        var columns = Sets.Select(s => s.Count == 1 ? s[0] : "(" + string.Join(", ", s) + ")");
        return $"{keyword}({string.Join(", ", columns)})";
    }
}

/// <summary>
/// The type of advanced grouping operation.
/// </summary>
public enum GroupingSetType
{
    /// <summary>GROUP BY ROLLUP(col1, col2, ...)</summary>
    Rollup,
    /// <summary>GROUP BY CUBE(col1, col2, ...)</summary>
    Cube,
    /// <summary>GROUP BY GROUPING SETS((col1), (col2), (), ...)</summary>
    GroupingSets
}
