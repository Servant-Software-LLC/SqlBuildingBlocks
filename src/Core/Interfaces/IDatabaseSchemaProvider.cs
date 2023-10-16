using SqlBuildingBlocks.QueryProcessing;

namespace SqlBuildingBlocks.Interfaces;

public interface IDatabaseSchemaProvider
{
    (bool DatabaseServiced, IEnumerable<SqlTableInfo> Tables) GetTables(string? database);

}
