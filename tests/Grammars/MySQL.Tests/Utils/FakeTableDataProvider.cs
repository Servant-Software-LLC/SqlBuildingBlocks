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

        if (table.TableName == "events_stages_history_long")
        {
            yield return new DataColumn("THREAD_ID", typeof(int));
            yield return new DataColumn("EVENT_NAME", typeof(string));
            yield return new DataColumn("NESTING_EVENT_ID", typeof(long));
            yield break;
        }

        throw new KeyNotFoundException();
    }

    public record variableValue(string VARIABLE_NAME, string VARIABLE_VALUE);
    public record events_stage_history(long THREAD_ID, string EVENT_NAME, long NESTING_EVENT_ID);

    private IEnumerable<variableValue> Variable_GetEnumerable()
    {
        yield return new("activate_all_roles_on_login", "OFF");
        yield return new("sql_mode", "STRICT_TRANS_TABLES,NO_ENGINE_SUBSTITUTION");
    }

    private IEnumerable<events_stage_history> EventsStageHistory_GetEnumerable()
    {
        yield break;
    }


    public IQueryable GetTableData(SqlTable table)
    {
        if (table.TableName == "session_variables")
        {
            return Variable_GetEnumerable().AsQueryable();
        }

        if (table.TableName == "events_stages_history_long")
        {
            return EventsStageHistory_GetEnumerable().AsQueryable();
        }

        throw new KeyNotFoundException();
    }

    public (bool DatabaseServiced, IEnumerable<SqlTableInfo> Tables) GetTables(string database)
    {
        throw new NotImplementedException();
    }
}
