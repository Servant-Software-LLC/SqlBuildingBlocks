using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using System.Data;

namespace SqlBuildingBlocks.Utils;

internal static class SelectDefinitionColumns
{
    public static IEnumerable<DataColumn> GetColumns(SqlTable table, ITableSchemaProvider tableSchemaProvider)
    {
        if (table is SqlDerivedTable derivedTable)
            return GetColumns(derivedTable.SelectDefinition);

        return tableSchemaProvider.GetColumns(table);
    }

    public static Type? GetColumnType(SqlTable table, string columnName, ITableSchemaProvider tableSchemaProvider)
    {
        var columns = GetColumns(table, tableSchemaProvider);
        return columns.FirstOrDefault(column => string.Compare(column.ColumnName, columnName, true) == 0)?.DataType;
    }

    public static IEnumerable<DataColumn> GetColumns(SqlSelectDefinition selectDefinition)
    {
        foreach (var column in selectDefinition.Columns)
        {
            if (column is SqlAllColumns allColumns)
            {
                if (allColumns.Columns == null)
                    continue;

                foreach (var expandedColumn in allColumns.Columns)
                    yield return new DataColumn(expandedColumn.ColumnAlias ?? expandedColumn.ColumnName, expandedColumn.ColumnType ?? typeof(object));

                continue;
            }

            if (column is ISqlColumnWithAlias columnWithAlias && !string.IsNullOrEmpty(columnWithAlias.ColumnAlias))
            {
                yield return new DataColumn(columnWithAlias.ColumnAlias!, GetColumnType(column) ?? typeof(object));
                continue;
            }

            if (!string.IsNullOrEmpty(column.ColumnName))
                yield return new DataColumn(column.ColumnName!, GetColumnType(column) ?? typeof(object));
        }
    }

    private static Type? GetColumnType(ISqlColumn column) =>
        column switch
        {
            SqlColumn sqlColumn => sqlColumn.ColumnType,
            SqlAggregate aggregate => aggregate.Argument?.Type,
            SqlFunctionColumn functionColumn => functionColumn.Function.ValueType,
            SqlLiteralValueColumn literalValueColumn => literalValueColumn.Value.GetType(),
            SqlParameterColumn => typeof(object),
            _ => typeof(object),
        };
}
