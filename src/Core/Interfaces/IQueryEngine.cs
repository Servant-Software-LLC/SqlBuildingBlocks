using System.Data;

namespace SqlBuildingBlocks.Interfaces;

public interface IQueryEngine
{
    (DataColumnCollection ColumnSchema, IEnumerable<DataRow> Results) Query();
}
