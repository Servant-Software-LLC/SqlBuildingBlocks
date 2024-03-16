using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.Utils;
using SqlBuildingBlocks.Visitors;
using System.Data;

namespace SqlBuildingBlocks.QueryProcessing;

public class QueryEngine : IQueryEngine
{
    private readonly ITableDataProvider tableDataProvider;
    private readonly SqlSelectDefinition sqlSelectDefinition;
    private readonly bool dataRowType = false;
    private readonly Lazy<DataTable> emptyDataTable = new Lazy<DataTable>(() => new DataTable());

    public QueryEngine(IAllTableDataProvider tableDataProvider, SqlSelectDefinition sqlSelectDefinition)
    {
        this.tableDataProvider = tableDataProvider ?? throw new ArgumentNullException(nameof(tableDataProvider));
        this.sqlSelectDefinition = sqlSelectDefinition ?? throw new ArgumentNullException(nameof(sqlSelectDefinition));
    }

    public QueryEngine(IEnumerable<DataSet> dataSets, SqlSelectDefinition sqlSelectDefinition)
    {
        tableDataProvider = new TableDataProviderAdaptor(dataSets);
        this.sqlSelectDefinition = sqlSelectDefinition ?? throw new ArgumentNullException(nameof(sqlSelectDefinition));
        dataRowType = true;
    }

    /// <summary>
    /// Executes the SELECT statement described in the <see cref="sqlSelectDefinition"/>.  Requires that by the time of
    /// calling this method, that all instances of <see cref="SqlParameter"/> and <see cref="SqlFunction"/> have been
    /// replaced in the <see cref="SqlExpression"/> instances with <see cref="SqlLiteralValue"/>.
    /// </summary>
    /// <returns>A DataColumnCollection which describes the schema of the columns of the SELECT query and an
    /// IEnumerable of DataRows, so that rows can be progressively fed back to the caller as they are calculated.</returns>
    public (DataColumnCollection ColumnSchema, IEnumerable<DataRow> Results) Query()
    {
        (ProcessingState processingState, IEnumerable<DataRow> selectRows) = QueryInternal();
        return (processingState.QueryOutput.Columns, selectRows);
    }

    /// Executes the SELECT statement described in the <see cref="sqlSelectDefinition"/>.  Requires that by the time of
    /// calling this method, that all instances of <see cref="SqlParameter"/> and <see cref="SqlFunction"/> have been
    /// replaced in the <see cref="SqlExpression"/> instances with <see cref="SqlLiteralValue"/>.
    /// </summary>
    /// <returns>A DataTable of the resultset.</returns>
    public DataTable QueryAsDataTable()
    {
        (ProcessingState processingState, IEnumerable<DataRow> selectRows) = QueryInternal();

        //Get all the rows and add them to this processing table.
        foreach (DataRow dataRow in selectRows)
        {
            processingState.QueryOutput.Rows.Add(dataRow);
        }

        return processingState.QueryOutput;        
    }

    private (ProcessingState processingState, IEnumerable<DataRow> SelectRows) QueryInternal()
    {
        ProcessingState processingState = new();
        DetermineColumns(processingState);

        var fromDataRows = GetQueryableRowsInFromTable(processingState);

        //If query has no FROM table.
        if (fromDataRows == null)
        {
            //TODO: Maltby - SQL Parser doesn't parse literals yet
            //This is a SELECT without any tables.  All literals and/or buildinFunctions.
            var dataRow = ResolveSelectColumns(processingState.QueryOutput);
            return (processingState, new DataRow[] { dataRow });
        }

        //If only a FROM with no JOINs.
        var onlyFromWithNoJoins = sqlSelectDefinition.Joins == null || sqlSelectDefinition.Joins.Count == 0;

        var selectRows = onlyFromWithNoJoins ?
                                    //FROM with no JOINs
                                    ResolveSelectColumns(processingState.QueryOutput, fromDataRows) :
                                    //FROM with JOINs
                                    ResolveSelectColumns(processingState, fromDataRows);

        //TODO:  Maltby - There are lots of optimization possibilities here in the table joins
        if (sqlSelectDefinition.Limit != null && sqlSelectDefinition.Limit.RowOffset.Value > 0)
            selectRows = selectRows.Skip(sqlSelectDefinition.Limit.RowOffset.Value);
        if (sqlSelectDefinition.Limit != null && sqlSelectDefinition.Limit.RowCount.Value > 0)
            selectRows = selectRows.Take(sqlSelectDefinition.Limit.RowCount.Value);

        //If this is a COUNT aggregate, then we only return 1 DataRow that has only the count itself.  (TODO: Technically, there could be other columns in this SELECT, but it hasn't been a needed use case yet)
        if (processingState.CountAggregate)
        {
            processingState.QueryOutput = new DataTable();
            processingState.QueryOutput.Columns.Add(new DataColumn("Count", typeof(int)));

            var onlyDataRow = processingState.QueryOutput.NewRow();
            onlyDataRow[0] = selectRows.Count();
            selectRows = new DataRow[] { onlyDataRow };
        }

        return (processingState, selectRows);
    }

    private IEnumerable<DataRow> ApplyFilter<TDataRow>(IQueryable<TDataRow> dataRows, SqlBinaryExpression filteringClause, Dictionary<SqlTable, DataRow> substituteValues, SqlTable tableDataRow, DataTable tableWithColumnsToProjectOnto)
    {
        var expression = filteringClause.BuildExpression<TDataRow>(substituteValues, tableDataRow);
        return dataRows.Where(expression).ToDataRows(tableWithColumnsToProjectOnto);
    }

    private IEnumerable<DataRow> ToDataRows<TDataRow>(IQueryable<TDataRow> dataRows, DataTable tableWithColumnsToProjectOnto) =>
        dataRows.ToDataRows(tableWithColumnsToProjectOnto);

    /// <summary>
    /// Resolves query output if there are no tables involved.
    /// </summary>
    /// <param name="dataTableOutput"></param>
    /// <returns></returns>
    private DataRow ResolveSelectColumns(DataTable queryOutput)
    {
        var selectColumns = queryOutput.NewRow();

        foreach (DataColumn dataColumn in queryOutput.Columns)
        {
            if (dataColumn.ExProps().TryGet(prop => prop.ValueResolver, out var valueResolver))
            {
                selectColumns[dataColumn.ColumnName] = valueResolver();
            }
        }

        return selectColumns;
    }

    /// <summary>
    /// Resolves query output if there is only a FROM table.
    /// </summary>
    /// <param name="queryOutput"></param>
    /// <param name="fromDataRows"></param>
    /// <returns></returns>
    private IEnumerable<DataRow> ResolveSelectColumns(DataTable queryOutput, IEnumerable<DataRow> fromDataRows)
    {
        //TODO: Maltby - Possible performance improvement.  Map columns of the SELECT to integer index values in fromDataRows' DataRow
        //               before enumerating all rows of fromDataRows.

        foreach (DataRow dataRow in fromDataRows)
        {
            var selectColumns = queryOutput.NewRow();
            foreach (DataColumn dataColumn in queryOutput.Columns)
            {
                //Items in the dataRow are using their original column names, so we need to get the original column name
                var sqlColumn = dataColumn.ExProps().Column;

                //Copy the column value from fromDataRows to the selectColumns. 
                selectColumns[dataColumn.ColumnName] = dataRow[GetColumnName(sqlColumn.ColumnName)];
            }

            yield return selectColumns;
        }
    }

    /// <summary>
    /// Resolves query output for both FROM and JOIN tables.
    /// </summary>
    /// <param name="processingState"></param>
    /// <param name="fromDataRows"></param>
    /// <returns></returns>
    private IEnumerable<DataRow> ResolveSelectColumns(ProcessingState processingState, IEnumerable<DataRow> fromDataRows)
    {
        foreach (DataRow dataRow in fromDataRows)
        {
            if (sqlSelectDefinition.Table is null)
                throw new ArgumentNullException(nameof(sqlSelectDefinition.Table), $"Cannot lookup table in processingState.DataRowsOfOtherTables because table value is null");

            processingState.DataRowsOfOtherTables[sqlSelectDefinition.Table] = dataRow;

            var enumerableQueryRows = ResolveSelectColumns(processingState, sqlSelectDefinition.Joins.ToArray());
            foreach (var queryRow in enumerableQueryRows)
                yield return queryRow;
        }
    }

    /// <summary>
    /// A recursive method called to resolve all the JOIN tables.
    /// </summary>
    /// <param name="processingState"></param>
    /// <param name="joinsToProcess"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private IEnumerable<DataRow> ResolveSelectColumns(ProcessingState processingState, SqlJoin[] joinsToProcess)
    {
        if (joinsToProcess.Length == 0)
            throw new Exception("Expected a JOIN to process");

        var joinInProcessing = joinsToProcess[0];
        var joinQueryable = GetQueryableRowsInJoinTable(processingState, joinInProcessing);
        foreach (DataRow dataRow in joinQueryable)
        {
            processingState.DataRowsOfOtherTables[joinInProcessing.Table] = dataRow;

            //If this is the last JOIN in the chain
            if (joinsToProcess.Length == 1)
            {
                var selectColumns = processingState.QueryOutput.NewRow();
                foreach (DataColumn dataColumn in processingState.QueryOutput.Columns)
                {
                    var extraProperties = dataColumn.ExProps();
                    var sourceColumnName = extraProperties.Column.ColumnName;
                    var outputColumnName = dataColumn.ColumnName;

                    if (extraProperties.TryGet(prop => prop.ValueResolver, out Func<object> valueResolver))
                    {
                        selectColumns[outputColumnName] = valueResolver();
                        continue;
                    }

                    if (extraProperties.TryGet(prop => prop.Table, out SqlTable selectColumnTable))
                    {
                        //Copy the column value from fromDataRows to the selectColumns
                        selectColumns[outputColumnName] = processingState.DataRowsOfOtherTables[selectColumnTable][GetColumnName(sourceColumnName)];
                    }

                }

                yield return selectColumns;
                continue;
            }

            //There are further JOINs, we need to recursively call down to them before we can compose the final query rows.
            var enumerableQueryRows = ResolveSelectColumns(processingState, joinsToProcess.Skip(1).ToArray());
            foreach (var queryRow in enumerableQueryRows)
                yield return queryRow;

        }

    }

    private IEnumerable<DataRow>? GetQueryableRowsInFromTable(ProcessingState processingState)
    {
        if (sqlSelectDefinition.Table is null)
            return null;

        if (!processingState.TablesProjections.TryGetValue(sqlSelectDefinition.Table, out var tableWithColumnsToProjectOnto))
            tableWithColumnsToProjectOnto = emptyDataTable.Value;

        var fromTableQueryable = tableDataProvider.GetTableData(sqlSelectDefinition.Table);
        if (fromTableQueryable is null)
            throw new ArgumentNullException($"Table '{sqlSelectDefinition.Table.TableName}' was not found in DataSet '{sqlSelectDefinition.Table.DatabaseName}'", nameof(fromTableQueryable));

        if (dataRowType)
            fromTableQueryable = fromTableQueryable.Cast<DataRow>();

        //Find out if we can limit the rows to use by applying the WHERE clause now.
        processingState.TablesInProcessing.Add(sqlSelectDefinition.Table);
        if (sqlSelectDefinition.WhereClause != null && WhereClauseContainsOnlyTables(sqlSelectDefinition.WhereClause, processingState.TablesInProcessing))
        {
            //Calls in a generic fashon:
            //  var expression = selectDefinition.WhereClause.BuildExpression<TDataRow>(null, selectDefinition.Table);
            //  return fromTableQueryable.Where(expression).ToDataRows(tableWithColumnsToProjectOnto);

            var applyFilterMethodReturnValue = ReflectionHelper.CallMethod<IEnumerable<DataRow>>(
                                                    this,
                                                    methodName: nameof(ApplyFilter),
                                                    typeParameter: fromTableQueryable.ElementType,
                                                    // Method arguments
                                                    fromTableQueryable, sqlSelectDefinition.WhereClause, null, sqlSelectDefinition.Table, tableWithColumnsToProjectOnto);

            if (applyFilterMethodReturnValue == null)
                throw new ArgumentNullException($"{nameof(ApplyFilter)}'s return value was null", innerException: null);

            processingState.WhereApplied = sqlSelectDefinition.Table;
            return applyFilterMethodReturnValue;
        }

        //Calls in a generic fashon:
        //  return fromTableQueryable.ToDataRows(tableWithColumnsToProjectOnto);
        var toDataRowsReturnValue = ReflectionHelper.CallMethod<IEnumerable<DataRow>>(
                                        this,
                                        methodName: nameof(ToDataRows),
                                        typeParameter: fromTableQueryable.ElementType,
                                        // Method arguments
                                        fromTableQueryable, tableWithColumnsToProjectOnto);

        if (toDataRowsReturnValue == null)
            throw new ArgumentNullException($"{nameof(ToDataRows)}'s return value was null", innerException: null);

        return toDataRowsReturnValue;
    }

    private IEnumerable<DataRow> GetQueryableRowsInJoinTable(ProcessingState processingState, SqlJoin join)
    {
        var joinTableQueryable = tableDataProvider.GetTableData(join.Table);
        if (joinTableQueryable is null)
            throw new ArgumentNullException($"Table '{join.Table.TableName}' was not found in DataSet '{join.Table.DatabaseName}'", nameof(joinTableQueryable));

        if (dataRowType)
            joinTableQueryable = joinTableQueryable.Cast<DataRow>();

        //var joinOnExpression = join.Condition.BuildExpression(processingState.DataRowsOfOtherTables, join.Table);

        var filteringExpression = join.Condition;

        //Find out if we can limit the rows to use by applying the WHERE clause now. (i.e. if WHERE isn't already used and doesn't contain tables further down in the JOINs)
        processingState.TablesInProcessing.Add(join.Table);
        if (sqlSelectDefinition.WhereClause != null &&
            processingState.WhereApplied is null &&
            WhereClauseContainsOnlyTables(sqlSelectDefinition.WhereClause, processingState.TablesInProcessing))
        {
            //Logically AND the JOIN ON and the WHERE
            filteringExpression = new SqlBinaryExpression(new(filteringExpression), SqlBinaryOperator.And, new(sqlSelectDefinition.WhereClause));

            processingState.WhereApplied = join.Table;
        }

        if (!processingState.TablesProjections.TryGetValue(join.Table, out var tableWithColumnsToProjectOnto))
            tableWithColumnsToProjectOnto = emptyDataTable.Value;

        //Calls in a generic fashon:
        //  var expression = filteringExpression.BuildExpression<TDataRow>(processingState.DataRowsOfOtherTables, join.Table);
        //  return joinTableQueryable.Where(expression).ToDataRows(tableWithColumnsToProjectOnto);

        var applyFilterMethodReturnValue = ReflectionHelper.CallMethod<IEnumerable<DataRow>>(
                                               this,
                                               methodName: nameof(ApplyFilter),
                                               typeParameter: joinTableQueryable.ElementType,
                                               // Method arguments
                                               joinTableQueryable, filteringExpression, processingState.DataRowsOfOtherTables, join.Table, tableWithColumnsToProjectOnto);

        if (applyFilterMethodReturnValue == null)
            throw new ArgumentNullException($"{nameof(ApplyFilter)}'s return value was null", innerException: null);

        processingState.WhereApplied = sqlSelectDefinition.Table;
        return applyFilterMethodReturnValue;
    }

    private bool WhereClauseContainsOnlyTables(SqlBinaryExpression whereClause, HashSet<SqlTable> tables)
    {
        //Check to see if the WHERE only contains the tables in processing
        ContainsTablesVisitor containsTablesVisitor = new ContainsTablesVisitor(tables);
        whereClause.Accept(containsTablesVisitor);

        return containsTablesVisitor.Result;
    }

    private void DetermineColumns(ProcessingState processingState)
    {
        bool firstColumnCountAggregate = sqlSelectDefinition.Columns.Count > 0 && sqlSelectDefinition.Columns[0] is SqlAggregate &&
                                         ((SqlAggregate)sqlSelectDefinition.Columns[0]).AggregateName == "COUNT";
        if (firstColumnCountAggregate)
        {
            if (sqlSelectDefinition.Columns.Count > 1)
                throw new InvalidOperationException("The aggregate COUNT can be the only column in the SELECT when used.");

            processingState.CountAggregate = true;
        }

        //Get a list of all column names that would have the same column name (i.e. a column with same name in two tables JOIN'd or where one conflicts with an alias) 
        var duplicateColumnNames = new HashSet<string>(sqlSelectDefinition.Columns
            .Where(col => !string.IsNullOrEmpty(col.ColumnName))
            .GroupBy(col => col.ColumnName)
            .Where(grouping => grouping.Count() > 1)
            .Select(grouping => grouping.Key)
            .OfType<string>());


        //Add all visible columns
        foreach (ISqlColumn iColumn in sqlSelectDefinition.Columns)
        {
            AddColumn(processingState, iColumn, duplicateColumnNames);
        }

        //If there are no JOINs then no values are substituted into the clauses and therefore, they don't need
        //projections.
        if (sqlSelectDefinition.Table is null || sqlSelectDefinition.Joins is null || sqlSelectDefinition.Joins.Count == 0)
            return;

        //Get all the ColumnRefs in the WHERE and JOIN ON clauses that will be substituting values
        //into the clauses.  (i.e. all tables except the last table in the JOIN chain)
        var lastJoinTable = sqlSelectDefinition.Joins.Last().Table;

        List<SqlColumnRef> columnRefsWithinClauses = new();
        if (sqlSelectDefinition.WhereClause != null)
            columnRefsWithinClauses.AddRange(GetColumnRefs(sqlSelectDefinition.WhereClause, lastJoinTable));

        foreach (SqlJoin join in sqlSelectDefinition.Joins)
        {
            columnRefsWithinClauses.AddRange(GetColumnRefs(join.Condition, lastJoinTable));
        }

        //TODO: Maltby - Later ORDER BY will need to be concatenated here too.  (GROUP BY?)

        //Try to add them into the projections if they aren't already there.
        foreach (var columnRef in columnRefsWithinClauses)
        {
            var column = (SqlColumn)columnRef.Column!;
            AddColumn(processingState, columnRef.ColumnName, null, column.ColumnType!,
                      false, columnRef.Column!, column.TableRef, true);
        }

    }

    /// <summary>
    /// Gets all <see cref="SqlColumnRef"/> that are part of the binary expression that are not the exclude table.
    /// </summary>
    /// <param name="sqlBinaryExpression"></param>
    /// <param name="excludeTable">Any <see cref="SqlColumnRef"/> that doesn't reference this table.</param>
    /// <returns></returns>
    private IList<SqlColumnRef> GetColumnRefs(SqlBinaryExpression sqlBinaryExpression, SqlTable excludeTable)
    {
        ColumnRefsVisitor columnRefsVisitor = new(new HashSet<SqlTable> { excludeTable }, false);
        sqlBinaryExpression.Accept(columnRefsVisitor);
        return columnRefsVisitor.Results;
    }

    private void AddAllColumns(ProcessingState processingState, SqlAllColumns allColumns)
    {
        if (allColumns.Columns == null)
            throw new ArgumentException($"AllColumns {allColumns} has null for its {nameof(allColumns.Columns)} property.");

        foreach (var column in allColumns.Columns)
        {
            if (column.ColumnType == null)
                throw new ArgumentException($"Column {column} has null for its {nameof(column.ColumnType)} property.");

            AddColumn(processingState, column.ColumnName, null, column.ColumnType, false, column, column.TableRef, false);
        }
    }

    private void AddColumn(ProcessingState processingState, ISqlColumn iColumn, HashSet<string> duplicateColumnNames)
    {
        switch (iColumn)
        {
            case SqlFunctionColumn functionColumn:
                if (functionColumn.Function.CalculateValue == null)
                    throw new Exception($"The {typeof(SqlFunction)} {functionColumn.Function} in the list of columns for this SQL statement, must either be calculated (i.e. replaced with a {nameof(SqlLiteralValue)}) before the {nameof(Query)} method is called, or it must have a lambda defined for its {nameof(SqlFunction.CalculateValue)} property which will calculate its value for each row of the resultset.");

                AddOutputColumn(processingState, functionColumn.ColumnName!, functionColumn.ColumnAlias, 
                                functionColumn.ColumnType, duplicateColumnNames.Contains(functionColumn.ColumnName!), 
                                functionColumn, null, functionColumn.Function.CalculateValue);
                break;

            case SqlColumn column:
                AddColumn(processingState, column.ColumnName, column.ColumnAlias, column.ColumnType ?? typeof(string), 
                          duplicateColumnNames.Contains(column.ColumnName), column, column.TableRef, false);
                break;

            case SqlAllColumns allColumns:
                AddAllColumns(processingState, allColumns);
                break;

            default:
                var aggregate = iColumn as SqlAggregate;

                if (aggregate == null || string.Compare(aggregate.AggregateName, "COUNT", true) != 0)
                    throw new Exception($"In trying to determine the Columns for SELECT output, there is no case that handles the type {iColumn.GetType().FullName}");
                break;
        }
    }

    private void AddColumn(ProcessingState processingState, string columnName, string? columnAlias, Type dataType,
                           bool duplicateColumnName, ISqlColumn column, SqlTable? associatedTable, bool hiddenColumn)
    {
        var dataColumnType = (dataType.IsGenericType && dataType.GetGenericTypeDefinition() == typeof(Nullable<>)) ?
                                dataType.GenericTypeArguments[0] :
                                dataType;

        if (!hiddenColumn)
            AddOutputColumn(processingState, columnName, columnAlias, dataColumnType, duplicateColumnName, column, associatedTable, null);

        if (associatedTable is not null)
        {
            if (!processingState.TablesProjections.TryGetValue(associatedTable, out DataTable dataTable))
            {
                dataTable = new DataTable();
                processingState.TablesProjections.Add(associatedTable, dataTable);
            }

            //Add the columnn to the projection.
            if (!dataTable.Columns.Contains(columnName))
            {
                DataColumn newTablesProjectionColumn = new();
                newTablesProjectionColumn.ColumnName = columnName;
                newTablesProjectionColumn.DataType = dataColumnType;

                dataTable.Columns.Add(newTablesProjectionColumn);
            }

        }

    }

    private static string GetOutputColumnName(string columnName, string? columnAlias, SqlTable? associatedTable, bool duplicateColumnName)
    {
        if (!string.IsNullOrEmpty(columnAlias))
            return columnAlias!;

        if (!duplicateColumnName)
            return columnName;

        if (associatedTable is null)
            throw new Exception($"Cannot create a resultset, there are multiple columns with the column name {columnName} and at least one of them does not have a table associated with it to prepend to the name of this result set column.");

        return $"{associatedTable.TableName}.{columnName}";
    }

    private static string GetColumnName(string? outputColumnName)
    {
        if (string.IsNullOrEmpty(outputColumnName))
            throw new ArgumentException($"The {nameof(outputColumnName)} parameter cannot be null or empty.");

        var indexOfLastPeriod = outputColumnName!.LastIndexOf('.');
        return indexOfLastPeriod < 0 ? outputColumnName : outputColumnName.Substring(indexOfLastPeriod + 1);
    }

    private void AddOutputColumn(ProcessingState processingState, string columnName, string? columnAlias, Type dataType, bool duplicateColumnName, ISqlColumn column, SqlTable? associatedTable, Func<object>? valueResolver)
    {
        DataColumn newColumn = new();
        newColumn.ColumnName = GetOutputColumnName(columnName, columnAlias, associatedTable, duplicateColumnName);
        newColumn.DataType = dataType;
        newColumn.ExProps().Column = column;

        if (associatedTable is not null)
        {
            newColumn.ExProps().Table = associatedTable;
        }

        if (valueResolver != null)
        {
            newColumn.ExProps().ValueResolver = valueResolver;
        }

        processingState.QueryOutput.Columns.Add(newColumn);
    }
}
