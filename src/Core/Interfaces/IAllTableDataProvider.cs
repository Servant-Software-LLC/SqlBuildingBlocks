namespace SqlBuildingBlocks.Interfaces;

/// <summary>
/// Marks a table-data provider that can serve data for <em>any</em> table in the schema,
/// not just a single pre-wired table.
///
/// <para>
/// <see cref="QueryEngine"/> accepts <see cref="IAllTableDataProvider"/> in its constructor
/// overloads that are intended for multi-table queries (JOINs, subqueries, CTEs). The
/// interface itself carries no additional members — it is a semantic marker that tells
/// <see cref="QueryEngine"/> the provider is capable of satisfying arbitrary
/// <see cref="ITableDataProvider.GetTableData"/> calls across the full schema, not just
/// a fixed, caller-supplied table.
/// </para>
///
/// <para>
/// The canonical implementation is <see cref="SqlBuildingBlocks.Utils.AllTableDataProvider"/>,
/// which aggregates multiple <see cref="ITableDataProvider"/> instances registered via
/// dependency injection and fans out <c>GetTableData</c> calls to the appropriate one.
/// </para>
///
/// <para>
/// <strong>Consumer note:</strong> Implement this interface (rather than
/// <see cref="ITableDataProvider"/> alone) when your provider is registered in a DI
/// container and can answer schema-wide data requests — for example, a MockDB
/// <c>SchemaManagerTableDataProvider</c> that holds all tables in memory and needs to
/// participate in JOIN execution.
/// </para>
/// </summary>
public interface IAllTableDataProvider : ITableDataProvider
{
}
