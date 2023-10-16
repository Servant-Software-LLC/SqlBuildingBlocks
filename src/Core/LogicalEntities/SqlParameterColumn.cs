using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlParameterColumn : ISqlColumn, ISqlColumnWithAlias
{
    public SqlParameterColumn(SqlParameter sqlParameter)
    {
        Parameter = sqlParameter ?? throw new ArgumentNullException(nameof(sqlParameter));
    }

    public SqlParameter Parameter { get; set; }

    public string? ColumnAlias { get; set; }

    public string? ColumnName => Parameter.Name;
}
