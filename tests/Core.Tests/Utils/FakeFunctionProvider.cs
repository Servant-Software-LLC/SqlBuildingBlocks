using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Core.Tests.Utils;

internal class FakeFunctionProvider : IFunctionProvider
{
    public Type GetDataType(SqlFunction sqlFunction) => 
        sqlFunction.FunctionName == "LAST_INSERT_ID" ? typeof(decimal) : typeof(int);

    public Func<object> GetDataValue(SqlFunction sqlFunction) =>
        sqlFunction.FunctionName == "ROW_COUNT" ? () => 2 :
        sqlFunction.FunctionName == "LAST_INSERT_ID" ? () => 3m :
        throw new KeyNotFoundException($"Unable to find function for the {nameof(FakeFunctionProvider)} which has a name of {sqlFunction.FunctionName}");
}
