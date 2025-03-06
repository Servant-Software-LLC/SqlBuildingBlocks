using SqlBuildingBlocks.POCOs;
using System.Data;
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

    [Fact]
    public void Indexer_CaseInsensitive()
    {
        /* Copy this behavior in our unit test

        DataSet dataSet = new DataSet();
        dataSet.Tables.Add(new DataTable("blogs"));

        var table = dataSet.Tables["Blogs"];
        Assert.NotNull(table);
        */

        VirtualDataSet dataSet = new();
        dataSet.Tables.Add(new VirtualDataTable("blogs"));

        var table = dataSet.Tables["Blogs"];
        Assert.NotNull(table);

    }

}
