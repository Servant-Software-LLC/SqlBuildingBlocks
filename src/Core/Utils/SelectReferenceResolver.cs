using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.LogicalEntities.BaseClasses;
using static SqlBuildingBlocks.Utils.TableFinder;

namespace SqlBuildingBlocks.Utils;

class SelectReferenceResolver
{
    private readonly SqlSelectDefinition sqlSelectDefinition;
    private readonly IDatabaseConnectionProvider databaseConnectionProvider;
    private readonly ITableSchemaProvider tableSchemaProvider;
    private readonly IFunctionProvider? functionProvider;

    public SelectReferenceResolver(SqlSelectDefinition sqlSelectDefinition, IDatabaseConnectionProvider databaseConnectionProvider, 
                                   ITableSchemaProvider tableSchemaProvider, IFunctionProvider? functionProvider)
    {
        this.sqlSelectDefinition = sqlSelectDefinition ?? throw new ArgumentNullException(nameof(sqlSelectDefinition));
        this.databaseConnectionProvider = databaseConnectionProvider ?? throw new ArgumentNullException(nameof(databaseConnectionProvider));
        this.tableSchemaProvider = tableSchemaProvider ?? throw new ArgumentNullException(nameof(tableSchemaProvider));
        this.functionProvider = functionProvider;
    }

    /// <summary>
    /// Provide the database name for each table if it wasn't specified in the SQL statement.
    /// </summary>
    public void ResolveTablesDatabase()
    {
        if (sqlSelectDefinition.Table is null)
            return;

        ResolveTablesDatabase(sqlSelectDefinition.Table, databaseConnectionProvider);

        foreach (var join in sqlSelectDefinition.Joins)
        {
            ResolveTablesDatabase(join.Table, databaseConnectionProvider);
        }
    }

    /// <summary>
    /// Provides relationships between all the columns, tables and WHERE/JOIN conditions that were parsed out in construction of this instance.
    /// </summary>
    public void ResolveReferences()
    {
        var tablesInSelect = sqlSelectDefinition.TablesInSelect;

        TableFinder columnNameToTables = new(tablesInSelect, tableSchemaProvider);

        DetermineTableReferencesOnColumns(tablesInSelect, columnNameToTables);
        if (sqlSelectDefinition.InvalidReferences)
            return;

        DetermineColumnReferencesOnJoinCondition(columnNameToTables);
        if (sqlSelectDefinition.InvalidReferences)
            return;

        DetermineColumnReferencesOnWhereConditions(columnNameToTables);
    }

    private void ResolveTablesDatabase(SqlTable sqlTable, IDatabaseConnectionProvider databaseConnectionProvider)
    {
        if (string.IsNullOrEmpty(sqlTable.DatabaseName))
        {
            var defaultDatabase = databaseConnectionProvider.DefaultDatabase;
            if (string.IsNullOrEmpty(defaultDatabase))
            {
                var errorMessage = $"The table {sqlTable} has no database specified and no default database has been provided.";
                sqlSelectDefinition.InvalidReferenceReason = errorMessage;
                return;
            }

            sqlTable.DatabaseName = defaultDatabase;
        }
    }

    private void DetermineTableReferencesOnColumns(IList<SqlTable> tablesInSelect, TableFinder columnNameToTables)
    {
        foreach (var selectColumn in sqlSelectDefinition.Columns)
        {
            switch (selectColumn)
            {
                case SqlFunctionColumn functionColumn:
                    if (functionProvider != null && functionColumn.Function != null)
                    {
                        functionColumn.Function.ValueType = functionProvider.GetDataType(functionColumn.Function);
                        functionColumn.Function.CalculateValue = functionProvider.GetDataValue(functionColumn.Function);
                    }
                    break;

                case SqlAggregate aggregate:
                    //NOTE:  No resolution is yet determined for columns that are arguments of an aggregate.
                    break;

                case SqlAllColumns allColumns:
                    DetermineAllColumnTableReferences(allColumns, tablesInSelect);
                    DetermineAllColumnColumns(allColumns);
                    break;

                case SqlColumn column:

                    //Determine if the table reference can be figured out without having to ask the caller for a list of columns
                    //for each table in the FROM/JOIN tables.
                    foreach (SqlTable table in tablesInSelect)
                    {
                        if ((tablesInSelect.Count == 1 && string.IsNullOrEmpty(column.TableName)) || ColumnReferencesTable(column, table))
                        {
                            var columnsInSchema = tableSchemaProvider.GetColumns(table);
                            if (columnsInSchema == null)
                            {
                                sqlSelectDefinition.InvalidReferenceReason = $"The {tableSchemaProvider.GetType()}(an {nameof(ITableSchemaProvider)}) instance returned null when calling the {nameof(tableSchemaProvider.GetColumns)} method for the table {table}";
                                return;
                            }

                            var columnInSchema = columnsInSchema.FirstOrDefault(col => col.ColumnName == column.ColumnName);
                            if (columnInSchema == null)
                            {
                                sqlSelectDefinition.InvalidReferenceReason = $"The column {column} in the SELECT statement either directly or indirectly references the table {table}, but the schema of this table does not contain this column.";
                                return;
                            }

                            column.ColumnType = columnInSchema.DataType;
                            column.TableRef = table;
                            break;
                        }
                    }

                    if (column.TableRef is not null)
                        break;

                    if (!string.IsNullOrEmpty(column.TableName))
                    {
                        sqlSelectDefinition.InvalidReferenceReason = $"The column {column} in the SELECT statement does not reference a table in the FROM/JOIN statements.";
                        return;
                    }

                    //The column doesn't specify a table name or alias, so we need to check if there are any tables that claim a column of this name.
                    var possibleTables = columnNameToTables.GetPossibleTables(column.ColumnName);

                    switch (possibleTables.Count)
                    {
                        case 0:
                            sqlSelectDefinition.InvalidReferenceReason = $"The column {column} in the SELECT statement does not directly refer to a table and no tables claim a column of this name in their schema.";
                            return;
                        case 1:
                            column.TableRef = possibleTables[0].Table;
                            column.ColumnType = possibleTables[0].ColumnType;
                            break;
                        default:
                            var possibleTableNames = string.Join(", ", possibleTables.Select(table => table.Table.TableName));
                            sqlSelectDefinition.InvalidReferenceReason = $"The column {column} in the SELECT statement does not directly refer to a table, but multiple tables claim a column of this name in their schema.  The column is amibiguous as a result.";
                            return;
                    }

                    break;

                default:
                    throw new Exception($"No case handles a SELECT column of type {selectColumn.GetType().FullName}");
            }
        }

    }

    private void DetermineColumnReferencesOnJoinCondition(TableFinder columnNameToTables)
    {
        if (sqlSelectDefinition.Joins == null || sqlSelectDefinition.Joins.Count == 0)
            return;

        foreach (var join in sqlSelectDefinition.Joins)
        {
            WalkBinaryExpression_SetColumnReferences(join.Condition, columnNameToTables, true);
        }
    }

    private void DetermineAllColumnTableReferences(SqlAllColumns allColumns, IList<SqlTable> tablesInSelect)
    {
        if (!string.IsNullOrEmpty(allColumns.TableName))
        {
            //If a table name is specified in the SELECT column, but it isn't the same as
            //the table name or alias in the FROM/JOIN, then it is an invalid reference.
            foreach (SqlTable table in tablesInSelect)
            {
                if (ColumnReferencesTable(string.Empty, allColumns.TableName, table))
                {
                    allColumns.TableRefs = new List<SqlTable>() { table };
                    break;
                }

            }

            if (allColumns.TableRefs == null)
            {
                sqlSelectDefinition.InvalidReferenceReason = $"The {allColumns} column in the SELECT statement does not reference a table in the FROM/JOIN statements.";
                return;
            }

        }
        else
        {
            allColumns.TableRefs = tablesInSelect;
        }

    }

    private void DetermineColumnReferencesOnWhereConditions(TableFinder columnNameToTables)
    {
        if (sqlSelectDefinition.WhereClause == null)
            return;

        WalkBinaryExpression_SetColumnReferences(sqlSelectDefinition.WhereClause, columnNameToTables, false);
    }

    private void WalkBinaryExpression_SetColumnReferences(SqlBinaryExpression binaryExpression, TableFinder columnNameToTables, bool joinOnClause)
    {
        SetColumnReferences(binaryExpression.Left, columnNameToTables, joinOnClause);
        SetColumnReferences(binaryExpression.Right, columnNameToTables, joinOnClause);
    }

    private void SetColumnReferences(SqlExpression operand, TableFinder columnNameToTables, bool joinOnClause)
    {
        if (operand.BinExpr != null)
        {
            WalkBinaryExpression_SetColumnReferences(operand.BinExpr, columnNameToTables, joinOnClause);
            return;
        }

        if (operand.Column != null)
        {
            //Check if this column references any of the columns in the SELECT Columns
            foreach (var column in sqlSelectDefinition.Columns.Where(iColumn => iColumn.GetType() == typeof(SqlColumn)).Cast<SqlColumn>())
            {
                if (operand.Column.RefersTo(column))
                {
                    operand.Column.Column = column;
                    return;
                }
            }

            //Check if there is an exact match on either a table alias or with a database prefixed name of the tables.
            (TableWithColumnType? matchedTable, string returnedInvalidReferenceReason) = columnNameToTables.GetMatchedTable(operand.Column);
            if (!string.IsNullOrEmpty(returnedInvalidReferenceReason))
            {
                sqlSelectDefinition.InvalidReferenceReason = returnedInvalidReferenceReason;
                return;
            }

            if (matchedTable is null)
            {
                matchedTable = HuntForPossibleTable(sqlSelectDefinition, operand, columnNameToTables, joinOnClause);
                if (matchedTable is null || sqlSelectDefinition.InvalidReferences)
                    return;
            }

            var columnBaseValues = GetColumnBaseValues(operand.Column.ColumnName, matchedTable.Table);
            SqlColumn hiddenColumn = new(columnBaseValues.DatabaseName, columnBaseValues.TableName, operand.Column.ColumnName)
            {
                ColumnType = matchedTable.ColumnType
            };
            hiddenColumn.TableRef = matchedTable.Table;
            operand.Column.Column = hiddenColumn;
        }
    }

    private (string? DatabaseName, string? TableName, string ColumnName) GetColumnBaseValues(string columnName, SqlTable table) =>
        new(table.DatabaseName, table.TableName, columnName);


    private TableWithColumnType? HuntForPossibleTable(SqlSelectDefinition sqlSelectDefinition, SqlExpression operand, TableFinder columnNameToTables, bool joinOnClause)
    {
        //Since it does not, then we need to try to find it in the user provided table schema.
        var possibleTables = columnNameToTables.GetPossibleTables(operand.Column!.ColumnName);

        var clauseType = joinOnClause ? "JOIN ON" : "WHERE";
        if (!string.IsNullOrEmpty(operand.Column.TableName))
        {
            var tableWithColumnTypeFound = possibleTables.FirstOrDefault(t => ColumnReferencesTable(operand.Column, t.Table));

            if (tableWithColumnTypeFound != null)
                return tableWithColumnTypeFound;

            sqlSelectDefinition.InvalidReferenceReason = $"The column {operand.Column} in the {clauseType} statement, specifies the table name {operand.Column.TableName}, but there is either no table with that name or the table does not have a column by this name.";
            return null;
        }

        if (possibleTables.Count == 0)
        {
            sqlSelectDefinition.InvalidReferenceReason = $"The column {operand.Column} in the {clauseType} statement does not directly refer to a column of the SELECT columns and no tables claim a column of this name in their schema.";
            return null;
        }

        if (possibleTables.Count > 1)
        {
            var possibleTableNames = string.Join(", ", possibleTables.Select(tableWithColumnType => tableWithColumnType.Table.TableName));
            sqlSelectDefinition.InvalidReferenceReason = $"The column {operand.Column} in the {clauseType} statement does not directly refer to a table, but multiple tables claim a column of this name in their schema.  The column is amibiguous as a result.";
            return null;
        }

        return possibleTables[0];
    }

    private bool ColumnReferencesTable(SqlColumnBase column, SqlTable table) =>
        ColumnReferencesTable(column.DatabaseName, column.TableName, table);

    private bool ColumnReferencesTable(string? columnsDatabaseName, string? columnsTableName, SqlTable? table)
    {
        if (table is null || string.IsNullOrEmpty(table.TableName))
            return false;

        var isDatabaseEmpty = string.IsNullOrEmpty(columnsDatabaseName);
        var isTablesDatabaseEmpty = string.IsNullOrEmpty(table.DatabaseName);
        var tableNamesMatch = string.Compare(columnsTableName, table.TableName, databaseConnectionProvider.CaseInsensitive) == 0;


        //If it is a table alias.
        if (isDatabaseEmpty && string.Compare(columnsTableName, table.TableAlias ?? string.Empty, databaseConnectionProvider.CaseInsensitive) == 0)
            return true;

        //If database names aren't specified then assume the same database
        if (isDatabaseEmpty)
            return tableNamesMatch;

        if (!isDatabaseEmpty && !isTablesDatabaseEmpty)
        {
            if (!tableNamesMatch)
                return false;

            return string.Compare(columnsDatabaseName, table.DatabaseName, databaseConnectionProvider.CaseInsensitive) == 0;
        }

        throw new Exception($"Unable to compare column reference to a table.  {nameof(columnsDatabaseName)}:{columnsDatabaseName} {nameof(table.DatabaseName)}:{table.DatabaseName} {nameof(columnsTableName)}:{columnsTableName} {nameof(table.TableName)}:{table.TableName} {nameof(table.TableAlias)}:{table.TableAlias}");
    }

    private void DetermineAllColumnColumns(SqlAllColumns allColumns)
    {
        allColumns.Columns = new List<SqlColumn>();

        if (allColumns.TableRefs == null)
        {
            sqlSelectDefinition.InvalidReferenceReason = $"Failed to call {nameof(DetermineAllColumnColumns)} because of null value for {nameof(allColumns.TableRefs)}";
            return;
        }

        foreach (SqlTable table in allColumns.TableRefs)
        {
            var columns = tableSchemaProvider.GetColumns(table);
            if (columns == null)
            {
                sqlSelectDefinition.InvalidReferenceReason = $"The {nameof(tableSchemaProvider)} returned null for getting columns of table {table}";
                return;
            }

            foreach (var column in columns)
            {
                var newColumn = new SqlColumn(table.DatabaseName, table.TableName, column.ColumnName);
                newColumn.TableRef = table;
                newColumn.ColumnType = column.DataType;

                allColumns.Columns.Add(newColumn);
            }
        }
    }

}
