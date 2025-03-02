using SqlBuildingBlocks.POCOs;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.POCOs;

public class VirtualDataTableCollectionTests
{
    private class DisposableVirtualDataTable(string name, Action disposeFunc) : VirtualDataTable(name), IDisposable
    {
        public void Dispose() => disposeFunc();
    }

    [Fact]
    public void Remove_DisposesVirtualDataTable()
    {
        const string tableName = "FakeTable";

        //Setup
        bool disposed = false;
        VirtualDataSet dataSet = new();
        dataSet.Tables.Add(new DisposableVirtualDataTable(tableName, () => disposed = true));

        //Act
        dataSet.Tables.Remove(tableName);

        Assert.True(disposed);
    }
}
