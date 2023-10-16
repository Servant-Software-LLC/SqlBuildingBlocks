using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using System.Data;

namespace SqlBuildingBlocks.Core.Tests.Utils;

internal class MeanTableSchemaProvider : ITableSchemaProvider
{
    public IEnumerable<DataColumn> GetColumns(SqlTable table) => null;
}
