using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Extensions;

public static class ITableSchemaProviderExtensions
{
    public static Type GetColumnType(this ITableSchemaProvider tableSchemaProvider, SqlTable table, string columnName)
    {
        var columns = tableSchemaProvider.GetColumns(table);
        var matchedColumn = columns.FirstOrDefault(columnInfo => string.Compare(columnInfo.ColumnName, columnName, true) == 0);
        return matchedColumn.DataType;
    }
}
