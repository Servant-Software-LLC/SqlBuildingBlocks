#nullable enable
using System.Collections;
using System.Reflection;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.Utils;

/// <summary>
/// Structural comparer for SqlBuildingBlocks AST nodes (issue #176 round-trip suite).
///
/// Walks two AST graphs in lockstep and reports the first divergence with a
/// diff-style path so test failures point at the exact mismatched property
/// rather than dumping two whole trees. The comparer:
///
///   • Compares public, readable, instance properties on logical-entity types.
///   • Recurses into properties whose declared type lives in
///     <see cref="SqlBuildingBlocks.LogicalEntities"/>.
///   • Compares <see cref="IEnumerable"/> values element-wise (length first, then
///     element-by-element under <c>[i]</c> path segments).
///   • Treats primitives, strings, enums, and types in <c>System.*</c> as leaf values
///     compared by <see cref="object.Equals(object?)"/>.
///
/// Intentionally narrow scope:
///   • Skips the <c>Cycles</c> / parent-back-pointer hazard by only recursing into
///     LogicalEntities-namespaced types and not chasing service / provider properties.
///   • Skips properties that throw on get (defensive — some lazy-resolved properties
///     can throw if references aren't resolved); the comparer logs the path and
///     treats both sides as equal-by-skip if both throw the same exception type.
/// </summary>
internal static class AstComparer
{
    /// <summary>
    /// Asserts two AST graphs are structurally equal. On mismatch the failure
    /// message includes the property path, expected, and actual values.
    /// </summary>
    public static void AssertEqual(object? expected, object? actual, string context)
    {
        var ctx = new ComparisonContext();
        Compare(expected, actual, "$", ctx);
        if (ctx.FirstDivergence != null)
        {
            Assert.Fail(
                $"AST round-trip mismatch ({context}){Environment.NewLine}" +
                $"  path     : {ctx.FirstDivergence.Path}{Environment.NewLine}" +
                $"  expected : {Render(ctx.FirstDivergence.Expected)}{Environment.NewLine}" +
                $"  actual   : {Render(ctx.FirstDivergence.Actual)}{Environment.NewLine}" +
                $"  reason   : {ctx.FirstDivergence.Reason}");
        }
    }

    private sealed class ComparisonContext
    {
        public Divergence? FirstDivergence { get; set; }
    }

    private sealed record Divergence(string Path, object? Expected, object? Actual, string Reason);

    private static void Compare(object? expected, object? actual, string path, ComparisonContext ctx)
    {
        if (ctx.FirstDivergence != null)
            return; // short-circuit once a divergence has been recorded

        if (expected == null && actual == null)
            return;

        if (expected == null || actual == null)
        {
            ctx.FirstDivergence = new Divergence(path, expected, actual, "one side is null, the other is not");
            return;
        }

        if (expected.GetType() != actual.GetType())
        {
            ctx.FirstDivergence = new Divergence(path, expected.GetType().Name, actual.GetType().Name, "runtime types differ");
            return;
        }

        var type = expected.GetType();

        // Leaf comparison for primitives, strings, enums, and System.* types.
        if (IsLeafType(type))
        {
            if (!Equals(expected, actual))
                ctx.FirstDivergence = new Divergence(path, expected, actual, "leaf values differ");
            return;
        }

        // Sequence comparison.
        if (expected is IEnumerable expectedEnumerable && actual is IEnumerable actualEnumerable)
        {
            CompareSequence(expectedEnumerable, actualEnumerable, path, ctx);
            return;
        }

        // Property-walk for LogicalEntities and similar AST node types.
        ComparePropertyWalk(expected, actual, path, ctx);
    }

    private static void CompareSequence(IEnumerable expected, IEnumerable actual, string path, ComparisonContext ctx)
    {
        var expectedList = expected.Cast<object?>().ToList();
        var actualList = actual.Cast<object?>().ToList();

        if (expectedList.Count != actualList.Count)
        {
            ctx.FirstDivergence = new Divergence(
                $"{path}.Count", expectedList.Count, actualList.Count, "sequence lengths differ");
            return;
        }

        for (int i = 0; i < expectedList.Count; i++)
        {
            Compare(expectedList[i], actualList[i], $"{path}[{i}]", ctx);
            if (ctx.FirstDivergence != null)
                return;
        }
    }

    private static void ComparePropertyWalk(object expected, object actual, string path, ComparisonContext ctx)
    {
        var type = expected.GetType();
        var properties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Where(p => ShouldRecurseIntoProperty(p));

        foreach (var prop in properties)
        {
            object? expectedValue;
            object? actualValue;
            try
            {
                expectedValue = prop.GetValue(expected);
            }
            catch (Exception expectedEx)
            {
                // If both sides throw the same exception type, treat as equal-by-skip.
                try
                {
                    prop.GetValue(actual);
                    ctx.FirstDivergence = new Divergence(
                        $"{path}.{prop.Name}", expectedEx.GetType().Name, "<no exception>", "expected getter threw, actual did not");
                }
                catch (Exception actualEx) when (actualEx.GetType() == expectedEx.GetType())
                {
                    // Both threw same exception — skip.
                }
                continue;
            }

            try
            {
                actualValue = prop.GetValue(actual);
            }
            catch (Exception actualEx)
            {
                ctx.FirstDivergence = new Divergence(
                    $"{path}.{prop.Name}", "<no exception>", actualEx.GetType().Name, "actual getter threw, expected did not");
                continue;
            }

            Compare(expectedValue, actualValue, $"{path}.{prop.Name}", ctx);
            if (ctx.FirstDivergence != null)
                return;
        }
    }

    private static bool ShouldRecurseIntoProperty(PropertyInfo prop)
    {
        // Skip indexer / getter on types that pull in service/provider graphs.
        // We only walk LogicalEntities and primitives. Properties whose type is
        // outside LogicalEntities AND outside System.* are skipped to avoid chasing
        // service references (IDatabaseConnectionProvider, ITableSchemaProvider, etc.)
        // pulled in via ResolveReferences.
        var t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        if (IsLeafType(t))
            return true;

        if (t.Namespace == "SqlBuildingBlocks.LogicalEntities" ||
            t.Namespace?.StartsWith("SqlBuildingBlocks.LogicalEntities.") == true)
            return true;

        // IEnumerable<T> where T is a logical entity or leaf — walk it.
        if (typeof(IEnumerable).IsAssignableFrom(t) && t != typeof(string))
            return true;

        // Skip everything else (interfaces from SqlBuildingBlocks.Interfaces,
        // System.Data classes, etc.).
        return false;
    }

    private static bool IsLeafType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t.IsPrimitive) return true;
        if (t.IsEnum) return true;
        if (t == typeof(string)) return true;
        if (t == typeof(decimal)) return true;
        if (t == typeof(DateTime)) return true;
        if (t == typeof(DateTimeOffset)) return true;
        if (t == typeof(TimeSpan)) return true;
        if (t == typeof(Guid)) return true;
        return false;
    }

    private static string Render(object? value)
    {
        if (value == null) return "<null>";
        if (value is string s) return $"\"{s}\"";
        return value.ToString() ?? "<null>";
    }
}
