using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.QueryProcessing;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.QueryProcessing;

public class TableDataProviderAdaptorTests
{
    [Fact]
    public void GetTableData_DoesntThrowIfMissingDataset()
    {
        TableDataProviderAdaptor tableDataProviderAdaptor = new TableDataProviderAdaptor();

        SqlTable sqlTable = new("BogusDatabase", "MyTable");
        var tableData = tableDataProviderAdaptor.GetTableData(sqlTable);
        Assert.Null(tableData);
    }
}
