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
    private readonly IList<SqlTable> outerTablesInScope;

    public SelectReferenceResolver(SqlSelectDefinition sqlSelectDefinition, IDatabaseConnectionProvider databaseConnectionProvider, 
                                   ITableSchemaProvider tableSchemaProvider, IFunctionProvider? functionProvider,
                                   IEnumerable<SqlTable>? outerTablesInScope = null)
    {
        this.sqlSelectDefinition = sqlSelectDefinition ?? throw new ArgumentNullException(nameof(sqlSelectDefinition));
        this.databaseConnectionProvider = databaseConnectionProvider ?? throw new ArgumentNullException(nameof(databaseConnectionProvider));
        this.tableSchemaProvider = tableSchemaProvider ?? throw new ArgumentNullException(nameof(tableSchemaProvider));
        this.functionProvider = functionProvider;
        this.outerTablesInScope = outerTablesInScope?.ToList() ?? new List<SqlTable>();
    }

    /// <summary>
    /// Provide the database name for each table if it wasn't specified in the SQL statement.
    /// </summary>
    public void ResolveTablesDatabase()
    {
        // Pre-resolve CTE bodies and rewrite FROM/JOIN tables that name a CTE so the
        // rest of the resolver pipeline sees the CTE as a known table-shaped scope
        // (mirroring SqlDerivedTable). Done first so ResolveTablesDatabase and column
        // resolution both see the rewritten tables.
        ResolveCtes();

        if (sqlSelectDefinition.Table is null)
            return;

        ResolveDerivedTables();

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
        // The main SELECT can reference any CTE declared in its WITH clause from inside
        // subqueries (EXISTS / scalar subquery / etc.), so add the resolved CTE tables
        // to the visible-from-outer scope alongside any pre-existing outer tables.
        var visibleTables = tablesInSelect.Concat(outerTablesInScope).ToList();

        TableFinder selectColumnTables = new(tablesInSelect, tableSchemaProvider);
        TableFinder visibleColumnTables = new(visibleTables, tableSchemaProvider);

        DetermineTableReferencesOnColumns(tablesInSelect, selectColumnTables);
        if (sqlSelectDefinition.InvalidReferences)
            return;

        DetermineColumnReferencesOnJoinCondition(visibleColumnTables, visibleTables);
        if (sqlSelectDefinition.InvalidReferences)
            return;

        DetermineColumnReferencesOnWhereConditions(visibleColumnTables, visibleTables);
    }

    private void ResolveTablesDatabase(SqlTable sqlTable, IDatabaseConnectionProvider databaseConnectionProvider)
    {
        if (sqlTable is SqlDerivedTable)
            return;

        // CTE-backed tables have no database — their schema is the CTE's projection.
        if (sqlTable is SqlCteTable)
            return;

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

                case SqlScalarSubqueryColumn scalarSubqueryColumn:
                    ResolveScalarSubqueryColumn(scalarSubqueryColumn, tablesInSelect.Concat(outerTablesInScope));
                    if (sqlSelectDefinition.InvalidReferences)
                        return;
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
                            var columnsInSchema = SelectDefinitionColumns.GetColumns(table, tableSchemaProvider);
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

    private void DetermineColumnReferencesOnJoinCondition(TableFinder columnNameToTables, IList<SqlTable> visibleTables)
    {
        if (sqlSelectDefinition.Joins == null || sqlSelectDefinition.Joins.Count == 0)
            return;

        foreach (var join in sqlSelectDefinition.Joins)
        {
            WalkBinaryExpression_SetColumnReferences(join.Condition, columnNameToTables, visibleTables, true);
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

    private void DetermineColumnReferencesOnWhereConditions(TableFinder columnNameToTables, IList<SqlTable> visibleTables)
    {
        if (sqlSelectDefinition.WhereClause == null)
            return;

        WalkExpression_SetColumnReferences(sqlSelectDefinition.WhereClause, columnNameToTables, visibleTables, false);
    }

    private void WalkBinaryExpression_SetColumnReferences(SqlBinaryExpression binaryExpression, TableFinder columnNameToTables, IList<SqlTable> visibleTables, bool joinOnClause)
    {
        SetColumnReferences(binaryExpression.Left, columnNameToTables, visibleTables, joinOnClause);
        if (binaryExpression.Right != null)
            SetColumnReferences(binaryExpression.Right, columnNameToTables, visibleTables, joinOnClause);
    }

    private void WalkBetweenExpression_SetColumnReferences(SqlBetweenExpression betweenExpression, TableFinder columnNameToTables, IList<SqlTable> visibleTables, bool joinOnClause)
    {
        SetColumnReferences(betweenExpression.Operand, columnNameToTables, visibleTables, joinOnClause);
        SetColumnReferences(betweenExpression.LowerBound, columnNameToTables, visibleTables, joinOnClause);
        SetColumnReferences(betweenExpression.UpperBound, columnNameToTables, visibleTables, joinOnClause);
    }

    private void WalkExpression_SetColumnReferences(SqlExpression expression, TableFinder columnNameToTables, IList<SqlTable> visibleTables, bool joinOnClause)
    {
        if (expression.BinExpr != null)
        {
            WalkBinaryExpression_SetColumnReferences(expression.BinExpr, columnNameToTables, visibleTables, joinOnClause);
            return;
        }

        if (expression.BetweenExpr != null)
        {
            WalkBetweenExpression_SetColumnReferences(expression.BetweenExpr, columnNameToTables, visibleTables, joinOnClause);
            return;
        }

        if (expression.ExistsExpr != null)
        {
            ResolveExistsExpression(expression.ExistsExpr, visibleTables);
            return;
        }

        if (expression.ScalarSubqueryExpr != null)
        {
            ResolveScalarSubqueryExpression(expression.ScalarSubqueryExpr, visibleTables);
            return;
        }

        SetColumnReferences(expression, columnNameToTables, visibleTables, joinOnClause);
    }

    private void SetColumnReferences(SqlExpression operand, TableFinder columnNameToTables, IList<SqlTable> visibleTables, bool joinOnClause)
    {
        if (operand.BinExpr != null)
        {
            WalkBinaryExpression_SetColumnReferences(operand.BinExpr, columnNameToTables, visibleTables, joinOnClause);
            return;
        }

        if (operand.BetweenExpr != null)
        {
            WalkBetweenExpression_SetColumnReferences(operand.BetweenExpr, columnNameToTables, visibleTables, joinOnClause);
            return;
        }

        if (operand.ExistsExpr != null)
        {
            ResolveExistsExpression(operand.ExistsExpr, visibleTables);
            return;
        }

        if (operand.ScalarSubqueryExpr != null)
        {
            ResolveScalarSubqueryExpression(operand.ScalarSubqueryExpr, visibleTables);
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
            var columns = SelectDefinitionColumns.GetColumns(table, tableSchemaProvider);
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

    private void ResolveDerivedTables()
    {
        foreach (var table in sqlSelectDefinition.TablesInSelect.OfType<SqlDerivedTable>())
        {
            table.SelectDefinition.ResolveReferences(databaseConnectionProvider, tableSchemaProvider, functionProvider);
        }
    }

    /// <summary>
    /// Pre-resolves each CTE in declaration order, then rewrites the main SELECT's
    /// FROM/JOIN tables that reference a CTE to <see cref="SqlCteTable"/> so the rest
    /// of the resolver treats them like derived tables. CTEs declared earlier in the
    /// same WITH clause are visible to later CTEs (per SQL semantics) and to the main
    /// SELECT's subqueries via <see cref="outerTablesInScope"/>.
    /// </summary>
    /// <remarks>
    /// For recursive CTEs (<see cref="SqlCteDefinition.IsRecursive"/> = true), the anchor
    /// body is resolved first to obtain a projection schema, the CTE is then registered
    /// as a <see cref="SqlCteTable"/>, and finally each recursive-term <see cref="SqlSetOperation"/>
    /// is resolved with the CTE itself in scope — enabling the recursive term to reference
    /// the CTE name in its FROM/JOIN.
    /// </remarks>
    private void ResolveCtes()
    {
        if (sqlSelectDefinition.Ctes.Count == 0)
            return;

        var caseInsensitive = databaseConnectionProvider.CaseInsensitive;
        var nameComparer = caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var cteTables = new Dictionary<string, SqlCteTable>(nameComparer);

        foreach (var cte in sqlSelectDefinition.Ctes)
        {
            // Inside the CTE body, a name matching a previously-declared CTE in this same
            // WITH clause should bind to that CTE rather than to a database table.
            RewriteCteReferences(cte.SelectDefinition, cteTables);

            // Resolve the anchor body against the running outer scope plus prior CTEs so
            // subqueries inside the body can also reference earlier CTEs. (The body's
            // SetOperations are not resolved here; for non-recursive CTEs the resolver
            // treats them as not-yet-resolved set operations; for recursive CTEs we
            // resolve them below with the CTE itself in scope.)
            var innerOuterScope = outerTablesInScope.Concat(cteTables.Values).ToList();
            cte.SelectDefinition.ResolveReferences(databaseConnectionProvider, tableSchemaProvider, functionProvider, innerOuterScope);

            if (cte.SelectDefinition.InvalidReferences)
            {
                sqlSelectDefinition.InvalidReferenceReason = cte.SelectDefinition.InvalidReferenceReason;
                return;
            }

            // Surface this CTE so subsequent CTEs and the main SELECT can resolve to it.
            var cteTable = new SqlCteTable(cte.Name, cte.SelectDefinition);
            cteTables[cte.Name] = cteTable;

            // Recursive CTE — now that the anchor's projection schema is known and the CTE
            // is registered, resolve the recursive-term right side(s) against a scope that
            // includes the CTE itself. The recursive term references the CTE by name in its
            // FROM/JOIN, which RewriteCteReferences then rewrites to the SqlCteTable.
            if (cte.IsRecursive)
            {
                ResolveRecursiveTerms(cte, cteTables, innerOuterScope);
                if (sqlSelectDefinition.InvalidReferences)
                    return;
            }
        }

        // Rewrite the main SELECT's FROM/JOIN tables so any reference to a CTE name
        // becomes a SqlCteTable carrying the resolved CTE projection.
        RewriteCteReferences(sqlSelectDefinition, cteTables);

        // Surface CTEs to subqueries inside the main SELECT (EXISTS / scalar subquery)
        // by augmenting the outer-scope list used by the rest of this resolver.
        foreach (var cteTable in cteTables.Values)
        {
            outerTablesInScope.Add(cteTable);
        }
    }

    /// <summary>
    /// Resolves each <see cref="SqlSetOperation"/> on a recursive CTE's body against a
    /// scope that includes the CTE itself, so the recursive term can reference the CTE
    /// name in its FROM/JOIN.
    /// </summary>
    private void ResolveRecursiveTerms(SqlCteDefinition cte, IDictionary<string, SqlCteTable> cteTables, List<SqlTable> outerScopeWithPriorCtes)
    {
        // Outer scope that the recursive term sees: the existing outer scope (which already
        // includes prior CTEs) plus this CTE itself (registered above by the caller).
        // cteTables already contains this CTE's entry, so no extra concat is needed.
        var recursiveOuterScope = outerTablesInScope.Concat(cteTables.Values).ToList();

        foreach (var setOp in cte.SelectDefinition.SetOperations)
        {
            RewriteCteReferences(setOp.Right, cteTables);
            setOp.Right.ResolveReferences(databaseConnectionProvider, tableSchemaProvider, functionProvider, recursiveOuterScope);

            if (setOp.Right.InvalidReferences)
            {
                sqlSelectDefinition.InvalidReferenceReason = setOp.Right.InvalidReferenceReason;
                return;
            }
        }
    }

    /// <summary>
    /// Replaces any <see cref="SqlTable"/> in <paramref name="select"/>'s FROM or JOIN
    /// position whose <see cref="SqlTable.TableName"/> matches a registered CTE with a
    /// <see cref="SqlCteTable"/> carrying the CTE's resolved <see cref="SqlSelectDefinition"/>.
    /// Already-rewritten <see cref="SqlCteTable"/> instances and <see cref="SqlDerivedTable"/>
    /// instances are left alone.
    /// </summary>
    private static void RewriteCteReferences(SqlSelectDefinition select, IDictionary<string, SqlCteTable> cteTables)
    {
        if (cteTables.Count == 0)
            return;

        if (select.Table is not null && select.Table is not SqlDerivedTable && select.Table is not SqlCteTable)
        {
            if (cteTables.TryGetValue(select.Table.TableName, out var cteTable))
            {
                select.Table = WrapCteTable(cteTable, select.Table);
            }
        }

        if (select.Joins is not null)
        {
            foreach (var join in select.Joins)
            {
                if (join.Table is SqlDerivedTable || join.Table is SqlCteTable)
                    continue;

                if (cteTables.TryGetValue(join.Table.TableName, out var cteTable))
                {
                    join.Table = WrapCteTable(cteTable, join.Table);
                }
            }
        }
    }

    /// <summary>
    /// Constructs a per-occurrence <see cref="SqlCteTable"/> that references the same
    /// resolved <see cref="SqlSelectDefinition"/> as the registered CTE but preserves
    /// this occurrence's <see cref="SqlTable.TableAlias"/>. Building a new instance
    /// (rather than reusing the registry instance) avoids alias-stomping when the same
    /// CTE is referenced multiple times with different aliases (e.g. self-joins).
    /// </summary>
    private static SqlCteTable WrapCteTable(SqlCteTable registered, SqlTable original)
    {
        return new SqlCteTable(registered.TableName, registered.SelectDefinition)
        {
            TableAlias = original.TableAlias,
        };
    }

    private void ResolveExistsExpression(SqlExistsExpression existsExpression, IEnumerable<SqlTable> visibleTables)
    {
        existsExpression.SelectDefinition.ResolveReferences(databaseConnectionProvider, tableSchemaProvider, functionProvider, visibleTables);

        if (existsExpression.SelectDefinition.InvalidReferences)
            sqlSelectDefinition.InvalidReferenceReason = existsExpression.SelectDefinition.InvalidReferenceReason;
    }

    private void ResolveScalarSubqueryExpression(SqlScalarSubqueryExpression scalarSubqueryExpression, IEnumerable<SqlTable> visibleTables)
    {
        ResolveScalarSubqueryDefinition(scalarSubqueryExpression.SelectDefinition, visibleTables);
        if (!scalarSubqueryExpression.SelectDefinition.InvalidReferences)
            scalarSubqueryExpression.ValueType = GetScalarSubqueryColumnType(scalarSubqueryExpression.SelectDefinition);
    }

    private void ResolveScalarSubqueryColumn(SqlScalarSubqueryColumn scalarSubqueryColumn, IEnumerable<SqlTable> visibleTables)
    {
        ResolveScalarSubqueryDefinition(scalarSubqueryColumn.SelectDefinition, visibleTables);
        if (!scalarSubqueryColumn.SelectDefinition.InvalidReferences)
            scalarSubqueryColumn.ColumnType = GetScalarSubqueryColumnType(scalarSubqueryColumn.SelectDefinition);
    }

    private void ResolveScalarSubqueryDefinition(SqlSelectDefinition scalarSubqueryDefinition, IEnumerable<SqlTable> visibleTables)
    {
        scalarSubqueryDefinition.ResolveReferences(databaseConnectionProvider, tableSchemaProvider, functionProvider, visibleTables);

        if (scalarSubqueryDefinition.InvalidReferences)
            sqlSelectDefinition.InvalidReferenceReason = scalarSubqueryDefinition.InvalidReferenceReason;
    }

    private static Type GetScalarSubqueryColumnType(SqlSelectDefinition scalarSubqueryDefinition) =>
        SelectDefinitionColumns.GetColumns(scalarSubqueryDefinition).FirstOrDefault()?.DataType ?? typeof(object);
}
