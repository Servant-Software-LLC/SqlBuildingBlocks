using SqlBuildingBlocks.LogicalEntities;
using System.Data;

namespace SqlBuildingBlocks.Interfaces;

public interface ITableSchemaProvider
{
    /// <summary>
    /// Gets a list of column names in their ordinal position relative to the table's schema
    /// </summary>
    /// <param name="table"></param>
    /// <returns>Null if order of columns is not provided.  (i.e. is determined by position of the properties in IQueryable.ElementType)</returns>
    IEnumerable<DataColumn> GetColumns(SqlTable table);
}
