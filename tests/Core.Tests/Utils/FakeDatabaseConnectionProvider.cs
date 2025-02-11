using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.Core.Tests.Utils;

public class FakeDatabaseConnectionProvider : IDatabaseConnectionProvider
{
    public string DefaultDatabase { get; set; } = string.Empty;

    public bool CaseInsensitive => true;
}
