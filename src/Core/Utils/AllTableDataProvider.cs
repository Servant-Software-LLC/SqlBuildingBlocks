using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.QueryProcessing;
using System.Data;

namespace SqlBuildingBlocks.Utils;

/// <summary>
/// Consolidates all registered ITableDataProvider services into one place.  Useful when table data providers span multiple projects or in plugable application scenarios.
/// 
/// Warning: Don't register this class as an ITableDataProvider service or infinite recursion will result.
/// </summary>
public class AllTableDataProvider : Consolidator<ITableDataProvider>, IAllTableDataProvider, ITableDataProvider
{
    private readonly IEnumerable<ITableDataProvider> tableDataProviders;

    public AllTableDataProvider(IEnumerable<ITableDataProvider> tableDataProviders)
    {
        this.tableDataProviders = tableDataProviders ?? throw new ArgumentNullException(nameof(tableDataProviders));
    }

    public (bool DatabaseServiced, IEnumerable<SqlTableInfo> Tables) GetTables(string? database) =>
        ConsolidateService(tableDataProviders, i => i.GetTables(database), $"There is no database named {database}",
                           resultFound: result => result.DatabaseServiced);

    public IQueryable GetTableData(SqlTable table) =>
        ConsolidateService(tableDataProviders, i => i.GetTableData(table), $"There is no table by the name of {table}",
                           resultFound: result => result != null);

    public IEnumerable<DataColumn> GetColumns(SqlTable table) =>
        ConsolidateService(tableDataProviders, i => i.GetColumns(table), string.Empty,
                           resultFound: result => result != null);
}
