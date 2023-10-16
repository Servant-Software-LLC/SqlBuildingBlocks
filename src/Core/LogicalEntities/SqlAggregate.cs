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

    public string? ColumnName => AggregateName;
}
