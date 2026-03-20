namespace SqlBuildingBlocks.LogicalEntities;

public class SqlMergeDefinition
{
    public SqlTable? TargetTable { get; set; }

    public SqlTable? SourceTable { get; set; }

    public SqlExpression? SearchCondition { get; set; }

    public IList<SqlMergeWhenClause> WhenClauses { get; } = new List<SqlMergeWhenClause>();

    /// <summary>
    /// SQL Server OUTPUT clause for capturing affected row values.
    /// </summary>
    public SqlOutputClause? OutputClause { get; set; }
}
