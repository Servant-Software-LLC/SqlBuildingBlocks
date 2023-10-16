using System.Data;

namespace SqlBuildingBlocks.Extensions;

public static class IQueryableExtensions
{
    public static IEnumerable<DataRow> ToDataRows<TSource>(this IQueryable<TSource> source, DataTable tableWithColumnsToProjectOnto) =>
        source.Select(source => ToDataRow(source, tableWithColumnsToProjectOnto)).AsEnumerable();

    //NOTE:  It is important that the second parameter here is the 'DataTable tableWithColumnsToProjectOnto', because DataRowQueryTranslator.VisitSelectLambda() is expecting it in this position.
    public static DataRow ToDataRow<TSource>(TSource source, DataTable tableWithColumnsToProjectOnto)
    {

        var newDataRow = tableWithColumnsToProjectOnto.NewRow();
        foreach (DataColumn dataColumn in tableWithColumnsToProjectOnto.Columns)
        {
            var propertyValue = GetValue(source, dataColumn.ColumnName);
            newDataRow[dataColumn.ColumnName] = propertyValue ?? DBNull.Value;
        }

        return newDataRow;
    }

    private static object GetValue<TSource>(TSource source, string columnName)
    {
        //If this is a DataRow..
        if (source is DataRow dataRow)
            return dataRow[columnName];

        //Use reflection to get the property value.
        var typeSource = typeof(TSource);
        var propertyInfo = typeSource.GetProperty(columnName);
        if (propertyInfo is null)
            throw new Exception($"Failed in {nameof(ToDataRow)}.  Column named {columnName} does not exist in {typeSource.FullName}");

        return propertyInfo.GetValue(source);
    }
}
