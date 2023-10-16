namespace SqlBuildingBlocks.Interfaces;

public interface IDatabaseConnectionProvider
{
    string DefaultDatabase { get; }

    bool CaseInsensitive { get; }
}
