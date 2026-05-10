using SqlBuildingBlocks.LogicalEntities;
using System.Collections.Concurrent;
using System.Data;

namespace SqlBuildingBlocks.Utils;

/// <summary>
/// Per-(predicate-shape, TDataRow, tableDataRow) compiled-delegate cache for
/// <see cref="SqlBinaryExpression.BuildCompiledPredicate{TDataRow}"/>.
///
/// Replaces the per-call <c>Expression.Lambda&lt;Func&lt;TDataRow,bool&gt;&gt;(...).Compile()</c>
/// inside the QueryEngine join hot path with a one-time compile per distinct
/// <c>(SqlBinaryExpression structural shape, TDataRow type, tableDataRow occurrence)</c> triple.
/// Subsequent calls invoke the cached delegate directly with the per-row substitute-values
/// dictionary supplied at runtime, instead of re-walking the expression tree and re-compiling
/// a fresh lambda whose only difference is the baked literal values.
///
/// See <see href="https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/188">#188</see>
/// — the Wave 12 reflection refactor (#129) eliminated MethodInfo.Invoke, but profiling
/// identified the surviving bottleneck on <c>ExecuteJoinedSelect</c> as the per-row predicate
/// compile inside <see cref="SqlBinaryExpression.BuildExpression{TDataRow}"/>.
///
/// Cache lifetime is process-wide; entries are never evicted. Predicate-shape diversity in a
/// real workload is bounded by the number of distinct WHERE / JOIN-ON clauses parsed by the
/// host process, which is a small constant for typical applications. If a consumer ever needs
/// eviction, switch the backing store to a bounded LRU — the public API does not need to change.
///
/// Key composition: <c>(structuralHash, TDataRow type, tableDataRow occurrence signature)</c>.
/// The structural hash is computed by walking the expression tree and combining
/// operator + arm-kind + column/table identity + literal types (NOT literal values — that is
/// the whole point of the cache). The <typeparamref name="TDataRow"/> type matters because
/// <see cref="SqlExpression.GetColumnExpression"/> emits different IL depending on whether the
/// row is a <see cref="DataRow"/> (indexer access) or a typed object (property access). The
/// tableDataRow is the SqlTable being filtered on; two predicates with the same shape but
/// different "this is the local table" assignment must compile to different lambdas because
/// the substitute-table check (<see cref="SqlTableOccurrenceComparer.Instance"/>) branches on it.
/// </summary>
internal static class CompiledPredicateCache
{
    /// <summary>
    /// Cache key — uses a structural fingerprint string (alias-aware, includes literal values)
    /// plus the row type and the table-being-filtered's occurrence signature. The string
    /// fingerprint is the source of truth for equality; the precomputed hash exists only to
    /// make the dictionary's hash bucket lookup fast. The hash alone is NOT sufficient — hash
    /// collisions would otherwise return the wrong cached lambda.
    /// </summary>
    private readonly struct Key : IEquatable<Key>
    {
        public readonly int Hash;
        public readonly string Fingerprint;
        public readonly Type TDataRow;
        public readonly SqlTable TableDataRow;

        public Key(int hash, string fingerprint, Type tDataRow, SqlTable tableDataRow)
        {
            Hash = hash;
            Fingerprint = fingerprint;
            TDataRow = tDataRow;
            TableDataRow = tableDataRow;
        }

        public bool Equals(Key other) =>
            Hash == other.Hash
            && string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal)
            && TDataRow == other.TDataRow
            && SqlTableOccurrenceComparer.Instance.Equals(TableDataRow, other.TableDataRow);

        public override bool Equals(object? obj) => obj is Key other && Equals(other);

        public override int GetHashCode() => Hash;
    }

    private static readonly ConcurrentDictionary<Key, Delegate> _cache = new();

    /// <summary>
    /// Looks up (or builds and caches) the compiled predicate for the given
    /// <paramref name="binaryExpression"/> shape, <typeparamref name="TDataRow"/>, and
    /// <paramref name="tableDataRow"/>. The <paramref name="builder"/> is invoked only on a
    /// cache miss and is responsible for producing the
    /// <c>Func&lt;TDataRow, Dictionary&lt;SqlTable, DataRow&gt;?, bool&gt;</c> delegate (typically
    /// by walking the expression tree once and compiling a runtime-substitute lambda).
    /// </summary>
    public static Func<TDataRow, Dictionary<SqlTable, DataRow>?, bool> GetOrAdd<TDataRow>(
        SqlBinaryExpression binaryExpression,
        SqlTable tableDataRow,
        Func<Func<TDataRow, Dictionary<SqlTable, DataRow>?, bool>> builder)
    {
        if (binaryExpression is null) throw new ArgumentNullException(nameof(binaryExpression));
        if (tableDataRow is null) throw new ArgumentNullException(nameof(tableDataRow));
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        var fingerprint = ComputeStructuralFingerprint(binaryExpression);
        var hash = (fingerprint, typeof(TDataRow), SqlTableOccurrenceComparer.Instance.GetHashCode(tableDataRow)).GetHashCode();
        var key = new Key(hash, fingerprint, typeof(TDataRow), tableDataRow);

        // ConcurrentDictionary.GetOrAdd is not strictly atomic — under contention the value
        // factory may run more than once. That is acceptable here because the result is
        // structurally identical regardless of which thread compiled it; only the wasted compile
        // is the cost. The dictionary still ends up with a single committed entry.
        var compiled = _cache.GetOrAdd(key, _ => builder());
        return (Func<TDataRow, Dictionary<SqlTable, DataRow>?, bool>)compiled;
    }

    /// <summary>
    /// Test-only hook: clears the cache so first-call compile cost can be re-observed.
    /// </summary>
    internal static void ClearForTests() => _cache.Clear();

    /// <summary>
    /// Test-only hook: returns the current cache size (number of distinct
    /// (predicate-shape, TDataRow, tableDataRow) tuples compiled so far).
    /// </summary>
    internal static int CacheCount => _cache.Count;

    // -----------------------------------------------------------------------
    // Structural fingerprint
    // -----------------------------------------------------------------------
    //
    // Captures: operator at every binary node, arm Kind, column identity (alias-aware), literal
    // CLR type AND value, IN-list arity, function name + arg arity. The string is the source of
    // truth for cache equality — two predicates with the same fingerprint always produce the
    // same compiled lambda, so cache hits are always semantically correct. The fingerprint is
    // built once per SqlBinaryExpression instance via a StringBuilder walk; subsequent lookups
    // pay only the string-equality cost.
    //
    // Why include literal values: the cached lambda bakes Expression.Constant for literal arms.
    // If we collapsed `WHERE Amount > 25` and `WHERE Amount > 250` to the same key, the second
    // call would reuse the first's compiled lambda with the wrong constant, returning incorrect
    // rows. (See cross-test failure observed during development: MySQL test ran
    // `WHERE Amount > 250.00` and cached the lambda; the AnsiSQL test ran `WHERE Amount > 25.00`
    // and got 0 rows because the cached lambda still compared against 250.00m.)

    internal static string ComputeStructuralFingerprint(SqlBinaryExpression binary)
    {
        if (binary is null) throw new ArgumentNullException(nameof(binary));

        var sb = new System.Text.StringBuilder();
        AppendBinary(sb, binary);
        return sb.ToString();
    }

    private static void AppendBinary(System.Text.StringBuilder sb, SqlBinaryExpression binary)
    {
        sb.Append('(');
        sb.Append((int)binary.Operator);
        sb.Append(' ');
        AppendExpression(sb, binary.Left);
        if (binary.Right != null)
        {
            sb.Append(' ');
            AppendExpression(sb, binary.Right);
        }
        sb.Append(')');
    }

    private static void AppendExpression(System.Text.StringBuilder sb, SqlExpression expression)
    {
        switch (expression.Kind)
        {
            case SqlExpressionKind.Column:
                {
                    var col = expression.Column!;
                    // ISqlColumn (the abstract base) does not expose TableRef — only the concrete
                    // SqlColumn does. Pattern-match so we get alias-aware identity for SqlColumn
                    // and fall back to column-name identity for other ISqlColumn implementations.
                    var sqlColumn = col.Column as SqlColumn;
                    var tableRef = sqlColumn?.TableRef;
                    sb.Append("Col[")
                      .Append(col.ColumnName?.ToLowerInvariant() ?? string.Empty)
                      .Append('|')
                      .Append(col.TableName?.ToLowerInvariant() ?? string.Empty)
                      .Append('|')
                      .Append(tableRef?.TableAlias?.ToLowerInvariant() ?? string.Empty)
                      .Append('|')
                      .Append(tableRef?.DatabaseName?.ToLowerInvariant() ?? string.Empty)
                      .Append(']');
                    break;
                }

            case SqlExpressionKind.Value:
                {
                    var v = expression.Value!;
                    var literalType =
                        v.String != null ? "s" :
                        v.Int != null ? "i" :
                        v.Float != null ? "f" :
                        v.Double != null ? "d" :
                        v.Decimal != null ? "m" :
                        v.Boolean != null ? "b" :
                        v.DBNull ? "n" :
                        "?";
                    sb.Append("Val[").Append(literalType).Append('|');
                    if (v.Value != null)
                    {
                        // Use invariant culture so numeric/decimal formatting is stable across
                        // locales — same predicate run on different machines must yield the same
                        // fingerprint.
                        if (v.Value is IFormattable formattable)
                            sb.Append(formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture));
                        else
                            sb.Append(v.Value.ToString());
                    }
                    sb.Append(']');
                    break;
                }

            case SqlExpressionKind.BinExpr:
                AppendBinary(sb, expression.BinExpr!);
                break;

            // Other arms — these are blocked by SqlBinaryExpression.IsCacheableShape() so a
            // fingerprint collision here cannot cause a wrong-result hit; non-cacheable
            // predicates never reach the cache. The Kind discriminator alone is sufficient.
            default:
                sb.Append("Other[").Append((int)expression.Kind).Append(']');
                break;
        }
    }
}
