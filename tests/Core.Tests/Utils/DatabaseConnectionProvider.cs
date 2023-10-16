using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.Core.Tests.Utils;

public class DatabaseConnectionProvider : IDatabaseConnectionProvider
{
    public string DefaultDatabase => "MyDatabase";

    public bool CaseInsensitive => true;
}
