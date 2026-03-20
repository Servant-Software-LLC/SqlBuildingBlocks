namespace SqlBuildingBlocks.LogicalEntities;

public class SqlOutputClause
{
    public IList<SqlOutputColumn> Columns { get; } = new List<SqlOutputColumn>();

    /// <summary>
    /// Optional INTO target table for captured output rows.
    /// Supports regular tables, table variables (@var), and temp tables (#tmp).
    /// </summary>
    public SqlTable? IntoTable { get; set; }
}
