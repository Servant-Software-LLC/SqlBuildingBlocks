using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.QueryProcessing;
using System.Data;

namespace SqlBuildingBlocks.Grammars.MySQL.Tests.Utils;

public class FakeTableDataProvider : ITableDataProvider
{
    public IEnumerable<DataColumn> GetColumns(SqlTable table)
    {
        if (table.TableName == "session_variables")
        {
            yield return new DataColumn("VARIABLE_NAME", typeof(string));
            yield return new DataColumn("VARIABLE_VALUE", typeof(string));
            yield break;
        }

        throw new NotImplementedException();
    }

    public record variable(string VARIABLE_NAME, string VARIABLE_VALUE);

    private IEnumerable<variable> GetEnumerable()
    {
        yield return new("activate_all_roles_on_login", "OFF");
        yield return new("sql_mode", "STRICT_TRANS_TABLES,NO_ENGINE_SUBSTITUTION");
    }
    public IQueryable GetTableData(SqlTable table)
    {
        var result = GetEnumerable().AsQueryable();
        return result;
    }

    public (bool DatabaseServiced, IEnumerable<SqlTableInfo> Tables) GetTables(string database)
    {
        throw new NotImplementedException();
    }
}
