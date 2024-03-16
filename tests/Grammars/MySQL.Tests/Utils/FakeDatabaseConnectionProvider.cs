using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.Grammars.MySQL.Tests.Utils;

internal class FakeDatabaseConnectionProvider : IDatabaseConnectionProvider
{
    public string DefaultDatabase { get; set; } = string.Empty;

    public bool CaseInsensitive => true;
}
