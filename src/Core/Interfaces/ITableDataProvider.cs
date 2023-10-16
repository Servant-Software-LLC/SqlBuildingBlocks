using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Interfaces;

public interface ITableDataProvider : IDatabaseSchemaProvider, ITableSchemaProvider
{
    IQueryable? GetTableData(SqlTable table);
}
