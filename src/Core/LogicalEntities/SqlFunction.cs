using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.QueryProcessing;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlFunction
{
    private static readonly Dictionary<string, WindowFunctionType> KnownWindowFunctions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ROW_NUMBER"] = WindowFunctionType.RowNumber,
            ["RANK"] = WindowFunctionType.Rank,
            ["DENSE_RANK"] = WindowFunctionType.DenseRank,
            ["NTILE"] = WindowFunctionType.Ntile,
            ["LAG"] = WindowFunctionType.Lag,
            ["LEAD"] = WindowFunctionType.Lead,
            ["FIRST_VALUE"] = WindowFunctionType.FirstValue,
            ["LAST_VALUE"] = WindowFunctionType.LastValue,
            ["NTH_VALUE"] = WindowFunctionType.NthValue,
        };

    public SqlFunction(string functionName)
    {
        FunctionName = functionName;
    }

    public string FunctionName { get; }
    public List<SqlExpression> Arguments { get; } = new List<SqlExpression>();

    public Type ValueType { get; set; } = typeof(string);

    /// <summary>
    /// If this instance, isn't replaced in the <see cref="SqlExpression" instances of a SQL statement before the
    /// <see cref="QueryEngine.Query"/> is called, then this property must be set in order for the <see cref="QueryEngine"/>
    /// to be able to calculate its value.  This situation normally arises when the context of this function is
    /// dependent on the columns specified in the <see cref="Arguments"/> property of this class.  For example, />
    /// <see href="https://learn.microsoft.com/en-us/sql/t-sql/functions/upper-transact-sql?view=sql-server-ver16">UPPER</see> </summary>
    public Func<object>? CalculateValue { get; set; }

    /// <summary>
    /// The optional OVER clause that makes this a window function.
    /// e.g. ROW_NUMBER() OVER (PARTITION BY dept ORDER BY salary DESC)
    /// </summary>
    public SqlWindowSpecification? WindowSpecification { get; set; }

    /// <summary>
    /// Whether this function has an OVER clause, making it a window function.
    /// </summary>
    public bool IsWindowFunction => WindowSpecification != null;

    /// <summary>
    /// Identifies this function as a well-known named window function (ROW_NUMBER, RANK, etc.),
    /// or <see cref="WindowFunctionType.None"/> if it is not a recognized window function name.
    /// This is determined by the function name regardless of whether an OVER clause is present.
    /// </summary>
    public WindowFunctionType WindowFunctionType =>
        KnownWindowFunctions.TryGetValue(FunctionName, out var type) ? type : WindowFunctionType.None;

    /// <summary>
    /// Whether this function's name is a recognized named window function
    /// (ROW_NUMBER, RANK, DENSE_RANK, NTILE, LAG, LEAD, FIRST_VALUE, LAST_VALUE, NTH_VALUE).
    /// </summary>
    public bool IsNamedWindowFunction => WindowFunctionType != WindowFunctionType.None;

    public SqlExpression? Accept(ISqlExpressionVisitor visitor) => visitor.Visit(this);

    public string ToExpressionString() => ToString();

    public override string ToString()
    {
        string arguments = string.Join(", ", Arguments.Select(arg => arg.ToString()));
        var result = $"{FunctionName}({arguments})";
        if (WindowSpecification != null)
            result += " " + WindowSpecification.ToString();
        return result;
    }
}
