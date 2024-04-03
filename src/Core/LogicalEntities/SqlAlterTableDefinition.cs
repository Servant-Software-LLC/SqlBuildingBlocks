namespace SqlBuildingBlocks.LogicalEntities;

public class SqlAlterTableDefinition
{
    public SqlTable? Table { get; set; }

    //NOTE:  Column names in ALTER TABLE, do not contain either the database name nor the schema name and hence
    //       do not derive from SqlColumn.  They are just strings.

    public IList<(SqlColumnDefinition Column, IList<SqlConstraintDefinition> Constraints)> ColumnsToAdd { get; private set; } = new List<(SqlColumnDefinition Column, IList<SqlConstraintDefinition> Constraints)>();

    public IList<string> ColumnsToDrop { get; private set; } = new List<string>();
}
