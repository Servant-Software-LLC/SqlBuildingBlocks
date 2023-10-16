using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlAllColumns : ISqlColumn
{
    /// <summary>
    /// Optional string which when not supplied implies all the columns from all tables involved
    /// in the SELECT query.
    /// </summary>
    public string? TableName { get; set;  }

    /// <summary>
    /// Once SelectDefinition.ResolveReferences() resolves, this list should always contain at least one element.
    /// </summary>
    public IList<SqlTable>? TableRefs { get; set; }

    /// <summary>
    /// Once SelectDefinition.ResolveReferences() resolve, this list should not be null.
    /// </summary>
    public IList<SqlColumn>? Columns { get; set; }

    public string? ColumnName => null;

    public override string ToString() => string.IsNullOrEmpty(TableName) ? "*" : $"{TableName}.*"; 
}
