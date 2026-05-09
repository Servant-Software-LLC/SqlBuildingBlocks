namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Represents a reference to a Common Table Expression (CTE) used as a table in a FROM/JOIN
/// clause. Carries the CTE's resolved <see cref="SqlSelectDefinition"/> so the resolver and
/// downstream column-matching machinery can derive a column schema from the CTE's projection
/// — mirroring how <see cref="SqlDerivedTable"/> exposes its inline subquery's projection.
/// </summary>
/// <remarks>
/// Unlike <see cref="SqlDerivedTable"/>, a CTE has a real name (the WITH-clause identifier)
/// rather than only an alias. The CTE name is stored as <see cref="SqlTable.TableName"/>; an
/// outer alias (e.g. <c>FROM cte AS c</c>) is preserved on <see cref="SqlTable.TableAlias"/>.
/// </remarks>
public class SqlCteTable : SqlTable
{
    public SqlCteTable(string cteName, SqlSelectDefinition selectDefinition)
        : base(null, cteName)
    {
        SelectDefinition = selectDefinition ?? throw new ArgumentNullException(nameof(selectDefinition));
    }

    public SqlSelectDefinition SelectDefinition { get; }

    public override string ToString() =>
        string.IsNullOrEmpty(TableAlias) ? $"<cte:{TableName}>" : $"<cte:{TableName}> AS {TableAlias}";
}
