namespace SqlBuildingBlocks.LogicalEntities;

public class SqlMergeWhenClause
{
    public SqlMergeWhenType WhenType { get; set; }

    public SqlExpression? AdditionalCondition { get; set; }

    public SqlMergeActionType ActionType { get; set; }

    /// <summary>
    /// SET assignments for UPDATE actions.
    /// </summary>
    public IList<SqlAssignment> Assignments { get; } = new List<SqlAssignment>();

    /// <summary>
    /// Column list for INSERT actions.
    /// </summary>
    public IList<SqlColumn> InsertColumns { get; } = new List<SqlColumn>();

    /// <summary>
    /// VALUES expressions for INSERT actions.
    /// </summary>
    public IList<SqlExpression> InsertValues { get; } = new List<SqlExpression>();
}
