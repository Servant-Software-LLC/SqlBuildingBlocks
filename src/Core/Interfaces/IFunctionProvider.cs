using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Interfaces;

public interface IFunctionProvider
{
    Type GetDataType(SqlFunction sqlFunction);

    Func<object> GetDataValue(SqlFunction sqlFunction);
}
