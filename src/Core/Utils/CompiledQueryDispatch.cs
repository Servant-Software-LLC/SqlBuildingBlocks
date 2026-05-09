using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.QueryProcessing;
using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace SqlBuildingBlocks.Utils;

/// <summary>
/// Per-element-type compiled delegate cache for the QueryEngine generic dispatch sites
/// (<c>ApplyFilter&lt;TDataRow&gt;</c> and <c>ToDataRows&lt;TDataRow&gt;</c>).
///
/// Replaces <see cref="ReflectionHelper.CallMethod{TResult}(object, string, Type, object?[])"/>
/// in the QueryEngine hot path. The first call for a given element type pays a one-time
/// compile cost; subsequent calls are direct delegate invocations (no MethodInfo.Invoke,
/// no TargetInvocationException wrapping).
///
/// See https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/129 — this was
/// implemented in Wave 12 of /uber-report 2026-05-09 after the BenchmarkDotNet baseline
/// (Docs/Benchmarks/baseline-2026-05-09-*.json) confirmed the reflection dispatch was a
/// real cost: ExecuteJoinedSelect was 185x slower than ExecuteSimpleSelect for two
/// 100-row tables.
/// </summary>
internal static class CompiledQueryDispatch
{
    // Delegate shape for ApplyFilter<TDataRow>:
    //   IEnumerable<DataRow> ApplyFilter(QueryEngine engine, IQueryable<TDataRow> dataRows,
    //                                    SqlBinaryExpression filteringClause,
    //                                    Dictionary<SqlTable, DataRow>? substituteValues,
    //                                    SqlTable tableDataRow,
    //                                    DataTable tableWithColumnsToProjectOnto)
    // The IQueryable<TDataRow> is passed as object and cast inside the compiled lambda,
    // so callers do not need to know TDataRow at compile time.
    private delegate IEnumerable<DataRow> ApplyFilterDelegate(
        QueryEngine engine,
        object dataRows,
        SqlBinaryExpression filteringClause,
        Dictionary<SqlTable, DataRow>? substituteValues,
        SqlTable tableDataRow,
        DataTable tableWithColumnsToProjectOnto);

    // Delegate shape for ToDataRows<TDataRow>:
    //   IEnumerable<DataRow> ToDataRows(IQueryable<TDataRow> dataRows,
    //                                   DataTable tableWithColumnsToProjectOnto)
    // Stateless — does not need the QueryEngine instance.
    private delegate IEnumerable<DataRow> ToDataRowsDelegate(
        object dataRows,
        DataTable tableWithColumnsToProjectOnto);

    private static readonly ConcurrentDictionary<Type, ApplyFilterDelegate> _applyFilterCache = new();
    private static readonly ConcurrentDictionary<Type, ToDataRowsDelegate> _toDataRowsCache = new();

    // Open generic SqlBinaryExpression.BuildExpression<TDataRow>(...) — resolved once.
    private static readonly MethodInfo _buildExpressionMethod = typeof(SqlBinaryExpression)
        .GetMethod(
            nameof(SqlBinaryExpression.BuildExpression),
            BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException(
            $"Could not resolve {nameof(SqlBinaryExpression)}.{nameof(SqlBinaryExpression.BuildExpression)} via reflection.");

    // Open generic Queryable.Where<TSource>(this IQueryable<TSource>, Expression<Func<TSource,bool>>) — resolved once.
    // We must pick the overload whose second parameter is Expression<Func<TSource, bool>>
    // (not the indexed Func<TSource, int, bool> overload).
    private static readonly MethodInfo _queryableWhereMethod = ResolveQueryableWhereMethod();

    // Open generic IQueryableExtensions.ToDataRows<TSource>(IQueryable<TSource>, DataTable) — resolved once.
    private static readonly MethodInfo _toDataRowsExtensionMethod = typeof(IQueryableExtensions)
        .GetMethod(
            nameof(IQueryableExtensions.ToDataRows),
            BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            $"Could not resolve {nameof(IQueryableExtensions)}.{nameof(IQueryableExtensions.ToDataRows)} via reflection.");

    /// <summary>
    /// Invokes the equivalent of <c>QueryEngine.ApplyFilter&lt;TDataRow&gt;(...)</c> via a
    /// cached compiled delegate keyed on <paramref name="elementType"/> (TDataRow).
    /// </summary>
    public static IEnumerable<DataRow> ApplyFilter(
        QueryEngine engine,
        Type elementType,
        object dataRows,
        SqlBinaryExpression filteringClause,
        Dictionary<SqlTable, DataRow>? substituteValues,
        SqlTable tableDataRow,
        DataTable tableWithColumnsToProjectOnto)
    {
        var compiled = _applyFilterCache.GetOrAdd(elementType, BuildApplyFilterDelegate);
        return compiled(engine, dataRows, filteringClause, substituteValues, tableDataRow, tableWithColumnsToProjectOnto);
    }

    /// <summary>
    /// Invokes the equivalent of <c>queryable.ToDataRows(table)</c> via a cached compiled
    /// delegate keyed on <paramref name="elementType"/> (TDataRow).
    /// </summary>
    public static IEnumerable<DataRow> ToDataRows(
        Type elementType,
        object dataRows,
        DataTable tableWithColumnsToProjectOnto)
    {
        var compiled = _toDataRowsCache.GetOrAdd(elementType, BuildToDataRowsDelegate);
        return compiled(dataRows, tableWithColumnsToProjectOnto);
    }

    /// <summary>
    /// Test-only hook: clears both caches so first-call compile cost can be observed.
    /// </summary>
    internal static void ClearForTests()
    {
        _applyFilterCache.Clear();
        _toDataRowsCache.Clear();
    }

    /// <summary>
    /// Test-only hook: returns the current ApplyFilter cache size (number of TDataRow
    /// types compiled so far). Useful for asserting that repeated calls reuse the same
    /// compiled delegate.
    /// </summary>
    internal static int ApplyFilterCacheCount => _applyFilterCache.Count;

    /// <summary>
    /// Test-only hook: returns the current ToDataRows cache size.
    /// </summary>
    internal static int ToDataRowsCacheCount => _toDataRowsCache.Count;

    private static ApplyFilterDelegate BuildApplyFilterDelegate(Type elementType)
    {
        // We rebuild the original ApplyFilter<TDataRow> body as an Expression so we
        // can compile a non-generic delegate that closes over TDataRow:
        //
        //   (engine, dataRowsObj, filteringClause, substituteValues, tableDataRow, table) =>
        //   {
        //       IQueryable<TDataRow> dataRows = (IQueryable<TDataRow>)dataRowsObj;
        //       Expression<Func<TDataRow, bool>> expr = filteringClause.BuildExpression<TDataRow>(substituteValues, tableDataRow);
        //       return dataRows.Where(expr).ToDataRows(table);
        //   }
        //
        // The QueryEngine 'engine' parameter is currently unused (ApplyFilter does not
        // touch instance state), but we keep it on the delegate so future evolution of
        // ApplyFilter does not require a cache schema change.

        var queryableType = typeof(IQueryable<>).MakeGenericType(elementType);

        var engineParam = Expression.Parameter(typeof(QueryEngine), "engine");
        var dataRowsObjParam = Expression.Parameter(typeof(object), "dataRowsObj");
        var filteringClauseParam = Expression.Parameter(typeof(SqlBinaryExpression), "filteringClause");
        var substituteValuesParam = Expression.Parameter(typeof(Dictionary<SqlTable, DataRow>), "substituteValues");
        var tableDataRowParam = Expression.Parameter(typeof(SqlTable), "tableDataRow");
        var tableParam = Expression.Parameter(typeof(DataTable), "tableWithColumnsToProjectOnto");

        // (IQueryable<TDataRow>)dataRowsObj
        var castDataRows = Expression.Convert(dataRowsObjParam, queryableType);

        // filteringClause.BuildExpression<TDataRow>(substituteValues, tableDataRow)
        var buildExpressionClosed = _buildExpressionMethod.MakeGenericMethod(elementType);
        var buildExpressionCall = Expression.Call(
            filteringClauseParam,
            buildExpressionClosed,
            substituteValuesParam,
            tableDataRowParam);

        // Queryable.Where<TDataRow>(dataRows, predicate)
        var whereClosed = _queryableWhereMethod.MakeGenericMethod(elementType);
        var whereCall = Expression.Call(whereClosed, castDataRows, buildExpressionCall);

        // IQueryableExtensions.ToDataRows<TDataRow>(filtered, table)
        var toDataRowsClosed = _toDataRowsExtensionMethod.MakeGenericMethod(elementType);
        var toDataRowsCall = Expression.Call(toDataRowsClosed, whereCall, tableParam);

        var lambda = Expression.Lambda<ApplyFilterDelegate>(
            toDataRowsCall,
            engineParam,
            dataRowsObjParam,
            filteringClauseParam,
            substituteValuesParam,
            tableDataRowParam,
            tableParam);

        return lambda.Compile();
    }

    private static ToDataRowsDelegate BuildToDataRowsDelegate(Type elementType)
    {
        // (dataRowsObj, table) =>
        //   IQueryableExtensions.ToDataRows<TDataRow>((IQueryable<TDataRow>)dataRowsObj, table);

        var queryableType = typeof(IQueryable<>).MakeGenericType(elementType);

        var dataRowsObjParam = Expression.Parameter(typeof(object), "dataRowsObj");
        var tableParam = Expression.Parameter(typeof(DataTable), "tableWithColumnsToProjectOnto");

        var castDataRows = Expression.Convert(dataRowsObjParam, queryableType);

        var toDataRowsClosed = _toDataRowsExtensionMethod.MakeGenericMethod(elementType);
        var toDataRowsCall = Expression.Call(toDataRowsClosed, castDataRows, tableParam);

        var lambda = Expression.Lambda<ToDataRowsDelegate>(
            toDataRowsCall,
            dataRowsObjParam,
            tableParam);

        return lambda.Compile();
    }

    private static MethodInfo ResolveQueryableWhereMethod()
    {
        // Queryable.Where has two overloads:
        //   Where<TSource>(this IQueryable<TSource>, Expression<Func<TSource, bool>>)
        //   Where<TSource>(this IQueryable<TSource>, Expression<Func<TSource, int, bool>>)
        // We want the first one — its predicate is Func<TSource, bool>.
        foreach (var method in typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != nameof(Queryable.Where))
                continue;
            if (!method.IsGenericMethodDefinition)
                continue;

            var parameters = method.GetParameters();
            if (parameters.Length != 2)
                continue;

            // The predicate parameter is Expression<Func<TSource, bool>>.
            // Check that the inner Func has 2 type arguments (TSource + return) — the
            // indexed overload has 3 (TSource + int + return).
            var predicateType = parameters[1].ParameterType;
            if (!predicateType.IsGenericType || predicateType.GetGenericTypeDefinition() != typeof(Expression<>))
                continue;

            var funcType = predicateType.GetGenericArguments()[0];
            if (funcType.IsGenericType && funcType.GetGenericArguments().Length == 2)
                return method;
        }

        throw new InvalidOperationException(
            "Could not resolve Queryable.Where<TSource>(this IQueryable<TSource>, Expression<Func<TSource, bool>>).");
    }
}
