using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlAggregate : ISqlColumn, ISqlColumnWithAlias
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="aggregateName"></param>
    /// <param name="argument">Value of null implies that an asterisk was provided</param>
    /// <exception cref="ArgumentNullException"></exception>
    public SqlAggregate(string aggregateName, SqlExpression? argument = null)
    {
        AggregateName = !string.IsNullOrEmpty(aggregateName) ? aggregateName : throw new ArgumentNullException(nameof(aggregateName));
        Argument = argument;
    }

    public string AggregateName { get; set; }

    public SqlExpression? Argument { get; set; }

    public string? ColumnAlias { get; set; }

    /// <summary>
    /// The optional OVER clause that makes this aggregate a window function.
    /// e.g. SUM(salary) OVER (PARTITION BY department_id)
    /// </summary>
    public SqlWindowSpecification? WindowSpecification { get; set; }

    /// <summary>
    /// Whether this aggregate has an OVER clause, making it a window aggregate.
    /// </summary>
    public bool IsWindowFunction => WindowSpecification != null;

    public string? ColumnName => AggregateName;
}
