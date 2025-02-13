using SqlBuildingBlocks.POCOs;

namespace SqlBuildingBlocks.Interfaces;

public interface IQueryEngine
{
    VirtualDataTable Query();
}
