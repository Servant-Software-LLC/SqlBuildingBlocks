using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Utils;

public class TableFinder
{
    public class TableWithColumnType
    {
        public SqlTable Table { get; }
        public Type ColumnType { get; }

        public TableWithColumnType(SqlTable table, Type columnType)
        {
            Table = table;
            ColumnType = columnType;
        }
    }

    private readonly IList<SqlTable> tables;
    private readonly ITableSchemaProvider tableDataProvider;

    /// <summary>
    /// Dictionary of column names to all tables that have a column with that name in it.
    /// </summary>
    private readonly Lazy<Dictionary<string, IList<TableWithColumnType>>> columnNameToPossibleTables;

    public TableFinder(IList<SqlTable> tables, ITableSchemaProvider tableDataProvider)
    {
        this.tables = tables ?? throw new ArgumentNullException(nameof(tables));
        this.tableDataProvider = tableDataProvider ?? throw new ArgumentNullException(nameof(tableDataProvider));

        columnNameToPossibleTables = new(() =>
        {
            Dictionary<string, IList<TableWithColumnType>> newInstance = new(StringComparer.OrdinalIgnoreCase);
            Load(newInstance, tables, tableDataProvider);
            return newInstance;
        });

    }

    public (TableWithColumnType? Table, string InvalidReferenceReason) GetMatchedTable(SqlColumnRef columnRef)
    {
        var matchedTable = GetMatchedTableInternal(columnRef);
        if (matchedTable.SqlTable is null)
            return new(null, matchedTable.InvalidReferenceReason);

        //Check the databases for this table.
        var columnType = tableDataProvider.GetColumnType(matchedTable.SqlTable, columnRef.ColumnName);
        if (columnType is null)
            return new(null, $"There is no column named {columnRef} in the {matchedTable}'s schema.");

        return new(new TableWithColumnType(matchedTable.SqlTable, columnType), string.Empty);
    }


    public IList<TableWithColumnType> GetPossibleTables(string columnName)
    {
        if (!columnNameToPossibleTables.Value.TryGetValue(columnName, out var tables))
        {
            return new List<TableWithColumnType>();
        }

        return tables;
    }

    private (SqlTable? SqlTable, string InvalidReferenceReason) GetMatchedTableInternal(SqlColumnRef columnRef)
    {
        var isDatabaseNameEmpty = string.IsNullOrEmpty(columnRef.DatabaseName);
        if (isDatabaseNameEmpty)
        {
            //Check if this column is using a table alias.
            foreach (var table in tables)
            {
                if (string.Compare(table.TableAlias, columnRef.TableName, true) == 0)
                    return new(table, string.Empty);
            }

            return new(null, string.Empty);
        }

        //The database name was provided.  See if we have a table that matches.
        foreach (var table in tables)
        {
            if (string.Compare(table.DatabaseName, columnRef.DatabaseName, true) == 0 &&
                string.Compare(table.TableName, columnRef.TableName, true) == 0)
                return new(table, string.Empty);
        }

        return new(null, $"Column reference {columnRef} specifies the database to which it belongs, but no tables in the FROM/JOIN specify a table from that database.");
    }


    private static void Load(Dictionary<string, IList<TableWithColumnType>> columnNameToPossibleTables, IList<SqlTable> tables, ITableSchemaProvider tableDataProvider)
    {
        foreach (var table in tables)
        {
            var columns = tableDataProvider.GetColumns(table);
            if (columns == null)
                continue;

            foreach (var column in columns)
            {
                AddRelationship(columnNameToPossibleTables, table, column.ColumnName, column.DataType);
            }
        }

    }

    private static void AddRelationship(Dictionary<string, IList<TableWithColumnType>> columnNameToPossibleTables, SqlTable table, string columnName, Type columnType)
    {
        if (!columnNameToPossibleTables.TryGetValue(columnName, out IList<TableWithColumnType> tablesWithName))
        {
            tablesWithName = new List<TableWithColumnType>();
            columnNameToPossibleTables.Add(columnName, tablesWithName);
        }

        tablesWithName.Add(new(table, columnType));
    }
}
