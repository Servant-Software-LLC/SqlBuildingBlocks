using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.POCOs;
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
    public VirtualDataTable Query()
    {
        (ProcessingState processingState, IEnumerable<DataRow> selectRows) = QueryInternal();

        processingState.QueryOutput.Rows = selectRows;
        return processingState.QueryOutput;
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
        processingState.QueryOutput.Rows = selectRows;

        return processingState.QueryOutput.ToDataTable();
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

        //If aggregates are present (with or without GROUP BY), evaluate them.
        if (processingState.HasAggregates)
        {
            if (sqlSelectDefinition.GroupBy != null && sqlSelectDefinition.GroupBy.Columns.Count > 0)
                return EvaluateGroupByAggregates(processingState, fromDataRows);
            return EvaluateAggregates(processingState, fromDataRows);
        }

        //If only a FROM with no JOINs.
        var onlyFromWithNoJoins = sqlSelectDefinition.Joins == null || sqlSelectDefinition.Joins.Count == 0;

        var selectRows = onlyFromWithNoJoins ?
                                    //FROM with no JOINs
                                    ResolveSelectColumnsFromTable(processingState, fromDataRows) :
                                    //FROM with JOINs
                                    ResolveSelectColumns(processingState, fromDataRows);

        //Apply DISTINCT before ORDER BY so that ordering operates on the deduplicated result set.
        if (sqlSelectDefinition.IsDistinct)
            selectRows = ApplyDistinct(selectRows, processingState.QueryOutput);

        //Apply ORDER BY before LIMIT/OFFSET so that LIMIT operates on the sorted result set.
        if (sqlSelectDefinition.OrderBy != null && sqlSelectDefinition.OrderBy.Count > 0)
            selectRows = ApplyOrderBy(selectRows, sqlSelectDefinition.OrderBy);

        //TODO:  Maltby - There are lots of optimization possibilities here in the table joins
        if (sqlSelectDefinition.Limit != null && sqlSelectDefinition.Limit.RowOffset.Value > 0)
            selectRows = selectRows.Skip(sqlSelectDefinition.Limit.RowOffset.Value);
        if (sqlSelectDefinition.Limit != null && sqlSelectDefinition.Limit.RowCount.Value > 0)
            selectRows = selectRows.Take(sqlSelectDefinition.Limit.RowCount.Value);

        return (processingState, selectRows);
    }

    private (ProcessingState processingState, IEnumerable<DataRow> SelectRows) EvaluateAggregates(ProcessingState processingState, IEnumerable<DataRow> sourceRows)
    {
        var allRows = sourceRows.ToList();
        processingState.QueryOutput = new("ResultSet");

        var values = new List<object>();
        foreach (var col in sqlSelectDefinition.Columns)
        {
            if (col is not SqlAggregate agg)
                throw new InvalidOperationException("When aggregates are present, all columns must be aggregates.");

            var aggValue = ComputeAggregate(agg, allRows);
            values.Add(aggValue ?? DBNull.Value);

            var columnName = agg.ColumnAlias ?? GetAggregateDefaultColumnName(agg);
            var dataType = GetAggregateColumnType(agg, aggValue);
            processingState.QueryOutput.Columns.Add(new DataColumn(columnName, dataType));
        }

        var resultRow = processingState.QueryOutput.NewRow();
        for (int i = 0; i < values.Count; i++)
            resultRow[i] = values[i];

        IEnumerable<DataRow> resultRows = new DataRow[] { resultRow };

        // Apply HAVING clause if present
        if (sqlSelectDefinition.HavingClause?.BinExpr != null)
            resultRows = ApplyHavingClause(resultRows, processingState.QueryOutput, sqlSelectDefinition.HavingClause.BinExpr, sqlSelectDefinition.Columns);

        return (processingState, resultRows);
    }

    private (ProcessingState processingState, IEnumerable<DataRow> SelectRows) EvaluateGroupByAggregates(ProcessingState processingState, IEnumerable<DataRow> sourceRows)
    {
        var allRows = sourceRows.ToList();
        var groupByColumns = sqlSelectDefinition.GroupBy!.Columns;

        // Group rows by the GROUP BY column values
        var groups = allRows.GroupBy(row =>
        {
            var keyParts = new object[groupByColumns.Count];
            for (int i = 0; i < groupByColumns.Count; i++)
            {
                var colName = GetColumnName(groupByColumns[i]);
                keyParts[i] = row.Table.Columns.Contains(colName) ? row[colName] : DBNull.Value;
            }
            return new GroupKey(keyParts);
        }).ToList();

        // Build output schema
        processingState.QueryOutput = new("ResultSet");
        var columnDefs = new List<(bool isAggregate, int groupByIndex, SqlAggregate? aggregate)>();

        foreach (var col in sqlSelectDefinition.Columns)
        {
            if (col is SqlAggregate agg)
            {
                var columnName = agg.ColumnAlias ?? GetAggregateDefaultColumnName(agg);
                // Determine type from first group
                var sampleValue = groups.Count > 0 ? ComputeAggregate(agg, groups[0].ToList()) : null;
                var dataType = GetAggregateColumnType(agg, sampleValue);
                processingState.QueryOutput.Columns.Add(new DataColumn(columnName, dataType));
                columnDefs.Add((true, -1, agg));
            }
            else
            {
                // Non-aggregate column — must be a GROUP BY column
                var colAlias = (col is ISqlColumnWithAlias colWithAlias) ? colWithAlias.ColumnAlias : null;
                var colName = colAlias ?? col.ColumnName!;
                var groupByIdx = -1;
                for (int i = 0; i < groupByColumns.Count; i++)
                {
                    if (string.Equals(GetColumnName(groupByColumns[i]), col.ColumnName, StringComparison.OrdinalIgnoreCase))
                    {
                        groupByIdx = i;
                        break;
                    }
                }

                var dataType = (col is SqlColumn sqlCol && sqlCol.ColumnType != null) ? sqlCol.ColumnType : typeof(object);
                if (dataType.IsGenericType && dataType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    dataType = dataType.GenericTypeArguments[0];
                processingState.QueryOutput.Columns.Add(new DataColumn(colName, dataType));
                columnDefs.Add((false, groupByIdx, null));
            }
        }

        // Produce one row per group
        var resultRows = new List<DataRow>();
        foreach (var group in groups)
        {
            var groupRows = group.ToList();
            var resultRow = processingState.QueryOutput.NewRow();

            for (int i = 0; i < columnDefs.Count; i++)
            {
                var def = columnDefs[i];
                if (def.isAggregate)
                {
                    var aggValue = ComputeAggregate(def.aggregate!, groupRows);
                    resultRow[i] = aggValue ?? DBNull.Value;
                }
                else if (def.groupByIndex >= 0)
                {
                    resultRow[i] = group.Key.Values[def.groupByIndex];
                }
                else
                {
                    // Non-aggregate, non-GROUP BY column — take first row's value
                    var colName = processingState.QueryOutput.Columns[i].ColumnName;
                    resultRow[i] = groupRows[0].Table.Columns.Contains(colName) ? groupRows[0][colName] : DBNull.Value;
                }
            }

            resultRows.Add(resultRow);
        }

        IEnumerable<DataRow> result = resultRows;

        // Apply HAVING clause if present
        if (sqlSelectDefinition.HavingClause?.BinExpr != null)
            result = ApplyHavingClause(result, processingState.QueryOutput, sqlSelectDefinition.HavingClause.BinExpr, sqlSelectDefinition.Columns);

        // Apply ORDER BY
        if (sqlSelectDefinition.OrderBy != null && sqlSelectDefinition.OrderBy.Count > 0)
            result = ApplyOrderBy(result, sqlSelectDefinition.OrderBy);

        return (processingState, result);
    }

    private static IEnumerable<DataRow> ApplyHavingClause(IEnumerable<DataRow> rows, VirtualDataTable queryOutput, SqlBinaryExpression havingClause, IList<ISqlColumn> selectColumns)
    {
        return rows.Where(row => EvaluateHavingPredicate(row, havingClause, selectColumns));
    }

    private static bool EvaluateHavingPredicate(DataRow row, SqlBinaryExpression expr, IList<ISqlColumn> selectColumns)
    {
        if (expr.Operator == SqlBinaryOperator.And)
        {
            var left = expr.Left.BinExpr != null && EvaluateHavingPredicate(row, expr.Left.BinExpr, selectColumns);
            var right = expr.Right?.BinExpr != null && EvaluateHavingPredicate(row, expr.Right.BinExpr, selectColumns);
            return left && right;
        }

        if (expr.Operator == SqlBinaryOperator.Or)
        {
            var left = expr.Left.BinExpr != null && EvaluateHavingPredicate(row, expr.Left.BinExpr, selectColumns);
            var right = expr.Right?.BinExpr != null && EvaluateHavingPredicate(row, expr.Right.BinExpr, selectColumns);
            return left || right;
        }

        // Get left and right values
        var leftValue = ResolveHavingValue(row, expr.Left, selectColumns);
        var rightValue = expr.Right != null ? ResolveHavingValue(row, expr.Right, selectColumns) : null;

        return CompareHavingValues(leftValue, rightValue, expr.Operator);
    }

    private static object? ResolveHavingValue(DataRow row, SqlExpression expr, IList<ISqlColumn> selectColumns)
    {
        if (expr.Value != null)
            return expr.Value.Value;

        if (expr.Column != null)
        {
            var colName = expr.Column.ColumnName;
            if (row.Table.Columns.Contains(colName))
                return row[colName];
        }

        // Aggregate function reference (e.g. COUNT(*), SUM(col)) — match against
        // the SELECT list aggregates to find the corresponding result column name.
        if (expr.Function != null)
        {
            var func = expr.Function;
            var funcName = func.FunctionName.ToUpperInvariant();

            // Match against SqlAggregate entries in the SELECT list
            foreach (var col in selectColumns)
            {
                if (col is SqlAggregate agg &&
                    string.Equals(agg.AggregateName, funcName, StringComparison.OrdinalIgnoreCase))
                {
                    var resultColName = agg.ColumnAlias ?? GetAggregateDefaultColumnName(agg);
                    if (row.Table.Columns.Contains(resultColName))
                        return row[resultColName];
                }
            }

            // Fallback: try the default aggregate column name directly
            string candidateName;
            if (func.Arguments.Count > 0 && func.Arguments[0].Column != null)
                candidateName = $"{funcName}({func.Arguments[0].Column.ColumnName})";
            else
                candidateName = funcName;

            if (row.Table.Columns.Contains(candidateName))
                return row[candidateName];
        }

        if (expr.BinExpr != null)
        {
            return null;
        }

        return null;
    }

    private static bool CompareHavingValues(object? left, object? right, SqlBinaryOperator op)
    {
        if (left == null || left == DBNull.Value || right == null || right == DBNull.Value)
            return false;

        var leftDecimal = Convert.ToDecimal(left);
        var rightDecimal = Convert.ToDecimal(right);

        return op switch
        {
            SqlBinaryOperator.Equal => leftDecimal == rightDecimal,
            SqlBinaryOperator.NotEqualTo => leftDecimal != rightDecimal,
            SqlBinaryOperator.LessThan => leftDecimal < rightDecimal,
            SqlBinaryOperator.LessThanEqual => leftDecimal <= rightDecimal,
            SqlBinaryOperator.GreaterThan => leftDecimal > rightDecimal,
            SqlBinaryOperator.GreaterThanEqual => leftDecimal >= rightDecimal,
            _ => false
        };
    }

    /// <summary>
    /// Key for grouping rows by GROUP BY column values.
    /// </summary>
    private class GroupKey : IEquatable<GroupKey>
    {
        public readonly object[] Values;

        public GroupKey(object[] values) => Values = values;

        public bool Equals(GroupKey? other)
        {
            if (other == null || Values.Length != other.Values.Length) return false;
            for (int i = 0; i < Values.Length; i++)
            {
                if (Values[i] == DBNull.Value && other.Values[i] == DBNull.Value) continue;
                if (Values[i] == DBNull.Value || other.Values[i] == DBNull.Value) return false;
                if (!Values[i].Equals(other.Values[i])) return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as GroupKey);

        public override int GetHashCode()
        {
            int hash = 17;
            foreach (var v in Values)
                hash = hash * 31 + (v == DBNull.Value ? 0 : v.GetHashCode());
            return hash;
        }
    }

    private object? ComputeAggregate(SqlAggregate aggregate, List<DataRow> rows)
    {
        var aggName = aggregate.AggregateName.ToUpperInvariant();

        if (aggName == "COUNT")
        {
            // COUNT(*) — count all rows; COUNT(col) — count non-null values
            if (aggregate.Argument?.Column == null)
                return rows.Count;

            var countColName = aggregate.Argument.Column.ColumnName;
            return rows.Count(r => r.Table.Columns.Contains(countColName) && r[countColName] != DBNull.Value);
        }

        var colName = GetAggregateColumnName(aggregate);
        var nonNullValues = rows
            .Where(r => r.Table.Columns.Contains(colName) && r[colName] != DBNull.Value)
            .Select(r => r[colName])
            .ToList();

        if (!nonNullValues.Any())
            return DBNull.Value;

        switch (aggName)
        {
            case "MAX":
                return nonNullValues.Cast<IComparable>().Max();
            case "MIN":
                return nonNullValues.Cast<IComparable>().Min();
            case "SUM":
                return nonNullValues.Select(v => Convert.ToDecimal(v)).Sum();
            case "AVG":
                return nonNullValues.Select(v => Convert.ToDecimal(v)).Average();
            default:
                throw new NotSupportedException($"Aggregate '{aggregate.AggregateName}' is not supported.");
        }
    }

    private static string GetAggregateDefaultColumnName(SqlAggregate aggregate)
    {
        if (aggregate.Argument?.Column != null)
            return $"{aggregate.AggregateName}({aggregate.Argument.Column.ColumnName})";

        return aggregate.AggregateName;
    }

    private static Type GetAggregateColumnType(SqlAggregate aggregate, object? aggValue)
    {
        var aggName = aggregate.AggregateName.ToUpperInvariant();

        // COUNT always returns int
        if (aggName == "COUNT")
            return typeof(int);

        // For MAX/MIN/SUM/AVG, use the source column's type if available
        if (aggregate.Argument?.Column?.Column is SqlColumn argCol && argCol.ColumnType != null)
        {
            var sourceType = argCol.ColumnType;
            // Unwrap Nullable<T>
            if (sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == typeof(Nullable<>))
                sourceType = sourceType.GenericTypeArguments[0];
            return sourceType;
        }

        // Fall back to the computed value's type
        if (aggValue != null && aggValue != DBNull.Value)
            return aggValue.GetType();

        return typeof(object);
    }

    private static string GetAggregateColumnName(SqlAggregate aggregate)
    {
        if (aggregate.Argument?.Column != null)
            return aggregate.Argument.Column.ColumnName;

        throw new NotSupportedException($"Aggregate '{aggregate.AggregateName}' requires a column argument. Use COUNT(*) for counting all rows.");
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
    private DataRow ResolveSelectColumns(VirtualDataTable queryOutput)
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
    /// Resolves query output if there is only a FROM table. Supports both regular columns
    /// and function columns with ValueResolver.
    /// </summary>
    private IEnumerable<DataRow> ResolveSelectColumnsFromTable(ProcessingState processingState, IEnumerable<DataRow> fromDataRows)
    {
        //TODO: Maltby - Possible performance improvement.  Map columns of the SELECT to integer index values in fromDataRows' DataRow
        //               before enumerating all rows of fromDataRows.

        foreach (DataRow dataRow in fromDataRows)
        {
            processingState.CurrentSourceRow = dataRow;
            var selectColumns = processingState.QueryOutput.NewRow();
            foreach (DataColumn dataColumn in processingState.QueryOutput.Columns)
            {
                var exProps = dataColumn.ExProps();

                if (exProps.TryGet(prop => prop.ValueResolver, out var valueResolver))
                {
                    selectColumns[dataColumn.ColumnName] = valueResolver();
                    continue;
                }

                //Items in the dataRow are using their original column names, so we need to get the original column name
                var sqlColumn = exProps.Column;

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
        var joins = sqlSelectDefinition.Joins.ToArray();

        // Pre-materialize all join table rows for RIGHT/FULL OUTER JOIN tracking
        foreach (var join in joins)
        {
            if (join.JoinKind == SqlJoinKind.Right || join.JoinKind == SqlJoinKind.Full)
            {
                var allRows = GetAllRowsInJoinTable(processingState, join);
                processingState.AllJoinTableRows[join.Table] = allRows;
                processingState.MatchedJoinRows[join.Table] = new bool[allRows.Count];
            }
        }

        foreach (DataRow dataRow in fromDataRows)
        {
            if (sqlSelectDefinition.Table is null)
                throw new ArgumentNullException(nameof(sqlSelectDefinition.Table), $"Cannot lookup table in processingState.DataRowsOfOtherTables because table value is null");

            processingState.DataRowsOfOtherTables[sqlSelectDefinition.Table] = dataRow;
            processingState.CurrentSourceRow = dataRow;

            var enumerableQueryRows = ResolveSelectColumns(processingState, joins);
            foreach (var queryRow in enumerableQueryRows)
                yield return queryRow;
        }

        // RIGHT/FULL OUTER JOIN: yield unmatched right-side rows with NULLs for left-side tables
        foreach (var join in joins)
        {
            if (join.JoinKind != SqlJoinKind.Right && join.JoinKind != SqlJoinKind.Full)
                continue;

            var allRows = processingState.AllJoinTableRows[join.Table];
            var matched = processingState.MatchedJoinRows[join.Table];

            // Collect all tables that should have NULL values (FROM table + all joins before this one)
            var nullTables = new HashSet<SqlTable>();
            if (sqlSelectDefinition.Table != null)
                nullTables.Add(sqlSelectDefinition.Table);
            foreach (var j in joins)
            {
                if (j == join) break;
                nullTables.Add(j.Table);
            }

            for (int i = 0; i < allRows.Count; i++)
            {
                if (!matched[i])
                {
                    processingState.DataRowsOfOtherTables[join.Table] = allRows[i];
                    yield return BuildResultRow(processingState, nullTables);
                }
            }
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
        var joinKind = joinInProcessing.JoinKind;
        var joinQueryable = GetQueryableRowsInJoinTable(processingState, joinInProcessing);
        bool hasMatch = false;

        foreach (DataRow dataRow in joinQueryable)
        {
            hasMatch = true;
            processingState.DataRowsOfOtherTables[joinInProcessing.Table] = dataRow;

            // Track matched rows for RIGHT/FULL OUTER JOIN
            if (processingState.MatchedJoinRows.TryGetValue(joinInProcessing.Table, out var matchedFlags))
                MarkRowMatched(dataRow, processingState.AllJoinTableRows[joinInProcessing.Table], matchedFlags);

            //If this is the last JOIN in the chain
            if (joinsToProcess.Length == 1)
            {
                yield return BuildResultRow(processingState);
                continue;
            }

            //There are further JOINs, we need to recursively call down to them before we can compose the final query rows.
            var enumerableQueryRows = ResolveSelectColumns(processingState, joinsToProcess.Skip(1).ToArray());
            foreach (var queryRow in enumerableQueryRows)
                yield return queryRow;

        }

        // LEFT/FULL OUTER JOIN: emit a row with NULLs for unmatched join table columns
        if (!hasMatch && (joinKind == SqlJoinKind.Left || joinKind == SqlJoinKind.Full))
        {
            // All tables from this join onward get NULL values
            var nullTables = new HashSet<SqlTable>(joinsToProcess.Select(j => j.Table));
            yield return BuildResultRow(processingState, nullTables);
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
        var whereClauseAsBinary = sqlSelectDefinition.WhereClause?.BinExpr;
        if (whereClauseAsBinary != null && WhereClauseContainsOnlyTables(whereClauseAsBinary, processingState.TablesInProcessing))
        {
            //Calls in a generic fashon:
            //  var expression = selectDefinition.WhereClause.BuildExpression<TDataRow>(null, selectDefinition.Table);
            //  return fromTableQueryable.Where(expression).ToDataRows(tableWithColumnsToProjectOnto);

            var applyFilterMethodReturnValue = ReflectionHelper.CallMethod<IEnumerable<DataRow>>(
                                                    this,
                                                    methodName: nameof(ApplyFilter),
                                                    typeParameter: fromTableQueryable.ElementType,
                                                    // Method arguments
                                                    fromTableQueryable, whereClauseAsBinary, null, sqlSelectDefinition.Table, tableWithColumnsToProjectOnto);

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
        var joinWhereClauseAsBinary = sqlSelectDefinition.WhereClause?.BinExpr;
        if (joinWhereClauseAsBinary != null &&
            processingState.WhereApplied is null &&
            WhereClauseContainsOnlyTables(joinWhereClauseAsBinary, processingState.TablesInProcessing))
        {
            //Logically AND the JOIN ON and the WHERE
            filteringExpression = new SqlBinaryExpression(new(filteringExpression), SqlBinaryOperator.And, new(joinWhereClauseAsBinary));

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

    /// <summary>
    /// Gets ALL rows from a join table without applying any filter (ON or WHERE).
    /// Used for RIGHT/FULL OUTER JOIN to identify unmatched rows.
    /// </summary>
    private List<DataRow> GetAllRowsInJoinTable(ProcessingState processingState, SqlJoin join)
    {
        var joinTableQueryable = tableDataProvider.GetTableData(join.Table);
        if (joinTableQueryable is null)
            throw new ArgumentNullException($"Table '{join.Table.TableName}' was not found in DataSet '{join.Table.DatabaseName}'", nameof(joinTableQueryable));

        if (dataRowType)
            joinTableQueryable = joinTableQueryable.Cast<DataRow>();

        if (!processingState.TablesProjections.TryGetValue(join.Table, out var tableWithColumnsToProjectOnto))
            tableWithColumnsToProjectOnto = emptyDataTable.Value;

        var toDataRowsReturnValue = ReflectionHelper.CallMethod<IEnumerable<DataRow>>(
                                        this,
                                        methodName: nameof(ToDataRows),
                                        typeParameter: joinTableQueryable.ElementType,
                                        // Method arguments
                                        joinTableQueryable, tableWithColumnsToProjectOnto);

        if (toDataRowsReturnValue == null)
            throw new ArgumentNullException($"{nameof(ToDataRows)}'s return value was null", innerException: null);

        return toDataRowsReturnValue.ToList();
    }

    /// <summary>
    /// Builds a result row from the current processing state. Columns belonging to tables in
    /// <paramref name="nullTables"/> are set to DBNull.Value (for OUTER JOIN unmatched rows).
    /// </summary>
    private DataRow BuildResultRow(ProcessingState processingState, IEnumerable<SqlTable>? nullTables = null)
    {
        var nullTableSet = nullTables != null ? new HashSet<SqlTable>(nullTables) : null;
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
                if (nullTableSet != null && nullTableSet.Contains(selectColumnTable))
                {
                    selectColumns[outputColumnName] = DBNull.Value;
                }
                else
                {
                    selectColumns[outputColumnName] = processingState.DataRowsOfOtherTables[selectColumnTable][GetColumnName(sourceColumnName)];
                }
            }
        }

        return selectColumns;
    }

    /// <summary>
    /// Marks a matched join row in the tracking array by finding it via value comparison.
    /// </summary>
    private static void MarkRowMatched(DataRow matchedRow, List<DataRow> allRows, bool[] matchedFlags)
    {
        for (int i = 0; i < allRows.Count; i++)
        {
            if (!matchedFlags[i] && RowValuesEqual(matchedRow, allRows[i]))
            {
                matchedFlags[i] = true;
                break;
            }
        }
    }

    private static bool RowValuesEqual(DataRow a, DataRow b)
    {
        var aItems = a.ItemArray;
        var bItems = b.ItemArray;
        if (aItems.Length != bItems.Length)
            return false;
        for (int i = 0; i < aItems.Length; i++)
        {
            if (!Equals(aItems[i], bItems[i]))
                return false;
        }
        return true;
    }

    private static IEnumerable<DataRow> ApplyOrderBy(IEnumerable<DataRow> rows, IList<SqlOrderByColumn> orderBy)
    {
        var first = orderBy[0];
        var firstColName = GetColumnName(first.ColumnName);

        IOrderedEnumerable<DataRow> ordered = first.Descending
            ? rows.OrderByDescending(row => row[firstColName], Comparer<object>.Create(CompareValues))
            : rows.OrderBy(row => row[firstColName], Comparer<object>.Create(CompareValues));

        for (int i = 1; i < orderBy.Count; i++)
        {
            var col = orderBy[i];
            var colName = GetColumnName(col.ColumnName);
            ordered = col.Descending
                ? ordered.ThenByDescending(row => row[colName], Comparer<object>.Create(CompareValues))
                : ordered.ThenBy(row => row[colName], Comparer<object>.Create(CompareValues));
        }

        return ordered;
    }

    private static IEnumerable<DataRow> ApplyDistinct(IEnumerable<DataRow> rows, VirtualDataTable queryOutput)
    {
        var columnNames = queryOutput.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var key = string.Join("\0", columnNames.Select(c =>
            {
                var val = row[c];
                return val == DBNull.Value ? "\x01NULL\x01" : val?.ToString() ?? "";
            }));

            if (seen.Add(key))
                yield return row;
        }
    }

    private static int CompareValues(object x, object y)
    {
        if (x == DBNull.Value && y == DBNull.Value) return 0;
        if (x == DBNull.Value) return -1;
        if (y == DBNull.Value) return 1;
        if (x is IComparable cx) return cx.CompareTo(y);
        return string.Compare(x.ToString(), y.ToString(), StringComparison.Ordinal);
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
        //Detect if any columns are aggregates
        bool hasAggregates = sqlSelectDefinition.Columns.OfType<SqlAggregate>().Any();
        if (hasAggregates)
        {
            processingState.HasAggregates = true;

            //For aggregates with column arguments, add the argument column as a hidden projection
            //so the column data is available for aggregate computation.
            foreach (var agg in sqlSelectDefinition.Columns.OfType<SqlAggregate>())
            {
                if (agg.Argument?.Column != null && agg.Argument.Column.Column is SqlColumn argSqlCol)
                {
                    var tableRef = argSqlCol.TableRef ?? sqlSelectDefinition.Table;
                    if (tableRef != null)
                    {
                        AddColumn(processingState, argSqlCol.ColumnName, null, argSqlCol.ColumnType ?? typeof(object),
                                  false, argSqlCol, tableRef, true);
                    }
                }
            }

            //For GROUP BY queries, also project the GROUP BY columns as hidden columns
            //so their data is available for grouping.
            if (sqlSelectDefinition.GroupBy != null)
            {
                foreach (var groupCol in sqlSelectDefinition.GroupBy.Columns)
                {
                    var colName = GetColumnName(groupCol);
                    // Find the matching SqlColumn in the select columns or create a hidden projection
                    var matchingCol = sqlSelectDefinition.Columns
                        .OfType<SqlColumn>()
                        .FirstOrDefault(c => string.Equals(c.ColumnName, colName, StringComparison.OrdinalIgnoreCase));

                    if (matchingCol != null)
                    {
                        var tableRef = matchingCol.TableRef ?? sqlSelectDefinition.Table;
                        if (tableRef != null)
                        {
                            AddColumn(processingState, matchingCol.ColumnName, null, matchingCol.ColumnType ?? typeof(object),
                                      false, matchingCol, tableRef, true);
                        }
                    }
                }
            }
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
        if (sqlSelectDefinition.WhereClause?.BinExpr != null)
            columnRefsWithinClauses.AddRange(GetColumnRefs(sqlSelectDefinition.WhereClause.BinExpr, lastJoinTable));

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
                {
                    var builtIn = TryCreateBuiltInFunction(processingState, functionColumn.Function);
                    if (builtIn == null)
                        throw new Exception($"The {typeof(SqlFunction)} {functionColumn.Function} in the list of columns for this SQL statement, must either be calculated (i.e. replaced with a {nameof(SqlLiteralValue)}) before the {nameof(Query)} method is called, or it must have a lambda defined for its {nameof(SqlFunction.CalculateValue)} property which will calculate its value for each row of the resultset.");

                    functionColumn.Function.CalculateValue = builtIn;
                }

                //For built-in functions with column arguments, ensure the argument column is projected
                EnsureFunctionArgumentsProjected(processingState, functionColumn.Function);

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

                if (aggregate == null)
                    throw new Exception($"In trying to determine the Columns for SELECT output, there is no case that handles the type {iColumn.GetType().FullName}");
                //Aggregates are handled in EvaluateAggregates, not as output columns.
                break;
        }
    }

    private Func<object>? TryCreateBuiltInFunction(ProcessingState processingState, SqlFunction function)
    {
        var funcName = function.FunctionName.ToUpperInvariant();
        switch (funcName)
        {
            case "UPPER":
                return () =>
                {
                    var argValue = ResolveArgumentValue(processingState, function.Arguments[0]);
                    if (argValue == null || argValue == DBNull.Value) return DBNull.Value;
                    return argValue.ToString()!.ToUpper();
                };
            case "LOWER":
                return () =>
                {
                    var argValue = ResolveArgumentValue(processingState, function.Arguments[0]);
                    if (argValue == null || argValue == DBNull.Value) return DBNull.Value;
                    return argValue.ToString()!.ToLower();
                };
            case "LENGTH":
            case "LEN":
                return () =>
                {
                    var argValue = ResolveArgumentValue(processingState, function.Arguments[0]);
                    if (argValue == null || argValue == DBNull.Value) return DBNull.Value;
                    return argValue.ToString()!.Length;
                };
            case "ABS":
                return () =>
                {
                    var argValue = ResolveArgumentValue(processingState, function.Arguments[0]);
                    if (argValue == null || argValue == DBNull.Value) return DBNull.Value;
                    return Math.Abs(Convert.ToDouble(argValue));
                };
            case "ROUND":
                return () =>
                {
                    var argValue = ResolveArgumentValue(processingState, function.Arguments[0]);
                    if (argValue == null || argValue == DBNull.Value) return DBNull.Value;
                    int digits = function.Arguments.Count > 1
                        ? Convert.ToInt32(ResolveArgumentValue(processingState, function.Arguments[1]))
                        : 0;
                    return Math.Round(Convert.ToDouble(argValue), digits);
                };
            default:
                return null;
        }
    }

    private static object? ResolveArgumentValue(ProcessingState processingState, SqlExpression arg)
    {
        if (arg.Value != null)
            return arg.Value.Value;

        if (arg.Column != null)
        {
            var row = processingState.CurrentSourceRow;
            if (row == null)
                throw new InvalidOperationException("No current row available for function evaluation.");
            return row[arg.Column.ColumnName];
        }

        throw new NotSupportedException("Function argument must be a column reference or literal value.");
    }

    private void EnsureFunctionArgumentsProjected(ProcessingState processingState, SqlFunction function)
    {
        foreach (var arg in function.Arguments)
        {
            if (arg.Column?.Column is SqlColumn argCol)
            {
                var tableRef = argCol.TableRef ?? sqlSelectDefinition.Table;
                if (tableRef != null)
                {
                    AddColumn(processingState, argCol.ColumnName, null, argCol.ColumnType ?? typeof(string),
                              false, argCol, tableRef, true);
                }
            }
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
            if (!dataTable.Columns.Cast<DataColumn>().Any(col => col.ColumnName == columnName))
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
