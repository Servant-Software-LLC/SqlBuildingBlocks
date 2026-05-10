namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Treats two <see cref="SqlTable"/> instances as the same key only when their database name,
/// table name AND alias all match. Used by the QueryEngine bookkeeping dictionaries and by the
/// JOIN ON / WHERE predicate-builder so that self-joins like <c>FROM t a JOIN t b ON a.k = b.k</c>
/// keep the two occurrences distinct.
///
/// Pre-issue #183 those dictionaries used the default <see cref="SqlTable.Equals(SqlTable?)"/>
/// implementation, which compares only <c>(DatabaseName, TableName)</c> and ignores
/// <see cref="SqlTable.TableAlias"/>. The two occurrences of <c>t</c> collapsed to the same key
/// and the engine produced a Cartesian product instead of the expected self-join semantics.
///
/// We do NOT change <see cref="SqlTable.Equals(SqlTable?)"/> itself — that contract is part of the
/// public API and consumers may rely on the alias-blind semantics for logical-table identity
/// checks (e.g., "is this column referencing the Customers table?"). Confining the alias-aware
/// comparison to a comparer keeps the blast radius minimal.
/// </summary>
public sealed class SqlTableOccurrenceComparer : IEqualityComparer<SqlTable>
{
    public static readonly SqlTableOccurrenceComparer Instance = new();

    public bool Equals(SqlTable? x, SqlTable? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        return string.Equals(x.DatabaseName, y.DatabaseName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.TableName, y.TableName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.TableAlias, y.TableAlias, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(SqlTable obj)
    {
        if (obj is null) return 0;
        // netstandard2.0 lacks System.HashCode; use a tuple-based combine. Lower-case so the hash
        // matches the case-insensitive Equals semantics above.
        return (
            obj.DatabaseName?.ToLowerInvariant(),
            obj.TableName?.ToLowerInvariant(),
            obj.TableAlias?.ToLowerInvariant()
        ).GetHashCode();
    }
}
