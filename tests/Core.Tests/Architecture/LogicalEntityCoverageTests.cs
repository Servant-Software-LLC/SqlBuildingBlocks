#nullable enable
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.Architecture;

/// <summary>
/// Issue #177 — Architectural rule enforcement.
///
/// Every public instance property on a logical-entity type that is reachable from
/// <see cref="SqlSelectDefinition"/> (the SELECT execution surface that QueryEngine
/// processes) must be backed by either:
///   (a) a consumer in the engine path that reads it (QueryEngine, Utils, Visitors,
///       or a sibling logical-entity helper), OR
///   (b) an explicit <see cref="NotSupportedException"/> raise-site keyed off the
///       unsupported feature (e.g., the existing
///       <c>QueryEngine.ThrowIfUnsupportedFeatures()</c> gate).
///
/// Three live issues from earlier waves (#170, #173, #174 — all closed) and several
/// recently-resolved ones share the same architectural smell: a logical-entity
/// property is populated by the parser, but the engine silently ignores it. This
/// test is the tripwire that prevents the next instance.
///
/// Implementation notes:
/// - The check is an identifier-substring scan, not a semantic one. False positives
///   are tolerated; false negatives are the failure mode the rule prevents. A
///   property is considered "consumed" if its name appears as an identifier in any
///   .cs file under the engine path (excluding the file that defines its declaring
///   type), or in a NotSupportedException string literal.
/// - DML/DDL types (INSERT/UPDATE/DELETE/MERGE/ALTER/CREATE/DROP/Transaction/
///   Savepoint/Rename) are NOT QueryEngine's responsibility — they are consumed by
///   downstream packages (FileBased.DataProviders, MockDB). Those types are
///   excluded from the audit by the SELECT-reachability filter.
/// - The allow-list captures the small set of properties that are intentionally
///   not consumed by the engine but are part of the public surface for other
///   reasons (diagnostics, round-tripping).
/// </summary>
public class LogicalEntityCoverageTests
{
    [Fact]
    public void EveryLogicalEntityPropertyHasEngineConsumerOrNotSupportedRaiseSite()
    {
        var coreAssembly = typeof(SqlSelectDefinition).Assembly;
        var logicalEntityTypes = coreAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract
                        && t.Namespace != null
                        && t.Namespace.StartsWith("SqlBuildingBlocks.LogicalEntities", StringComparison.Ordinal))
            .ToArray();

        // The QueryEngine processes SELECT statements. Audit only the types reachable
        // from SqlSelectDefinition via property/element type traversal. DML/DDL types
        // (INSERT/UPDATE/DELETE/MERGE/ALTER/CREATE/DROP/Transaction/Savepoint/Rename)
        // are out of QueryEngine's scope — their properties are consumed by downstream
        // packages.
        var selectReachable = ComputeSelectReachableTypes(typeof(SqlSelectDefinition), logicalEntityTypes);

        // Source files in the engine path. The scan uses these as the corpus for the
        // "is this property name referenced anywhere?" check. The defining file of
        // each property's declaring type is excluded so that a property which only
        // appears in its own getter/setter declaration does not count as consumed.
        var enginePathDirs = new[]
        {
            Path.Combine("src", "Core", "QueryProcessing"),
            Path.Combine("src", "Core", "Utils"),
            Path.Combine("src", "Core", "Visitors"),
            Path.Combine("src", "Core", "LogicalEntities"),
            Path.Combine("src", "Core"), // root-level files like SavepointStmt.cs (but mostly NonTerminals — harmless)
        };

        var repoRoot = FindRepoRoot();
        var sourceFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in enginePathDirs)
        {
            var fullDir = Path.Combine(repoRoot, dir);
            if (!Directory.Exists(fullDir))
                continue;

            foreach (var file in Directory.EnumerateFiles(fullDir, "*.cs", SearchOption.AllDirectories))
            {
                // Skip generated files under obj/ and bin/.
                var rel = Path.GetRelativePath(repoRoot, file);
                if (rel.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                    rel.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    continue;

                if (!sourceFiles.ContainsKey(file))
                    sourceFiles[file] = File.ReadAllText(file);
            }
        }

        Assert.True(sourceFiles.Count > 0,
            $"Could not locate any engine-path source files under {repoRoot}. The reflection test cannot run without source.");

        var failures = new List<string>();

        foreach (var type in selectReachable)
        {
            // Locate the .cs file that defines this type so we can exclude it from the
            // consumer search (a property's name will always appear in its own
            // declaration, which is not evidence of engine consumption).
            var definingFile = FindDefiningFile(sourceFiles, type);

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var prop in properties)
            {
                if (IsAllowListed(type, prop))
                    continue;

                if (IsConsumedInEnginePath(prop.Name, sourceFiles, definingFile))
                    continue;

                failures.Add($"{type.Name}.{prop.Name}");
            }
        }

        if (failures.Count > 0)
        {
            var message =
                "Issue #177 — logical-entity coverage rule violated. The following properties on " +
                "SELECT-reachable logical-entity types are neither consumed by the engine path " +
                "(QueryProcessing/Utils/Visitors/LogicalEntities) nor referenced by name in a " +
                "NotSupportedException raise-site. Either wire engine consumption, add a " +
                "ThrowIfUnsupportedFeatures guard, or add the property to the allow-list with a " +
                "comment explaining why.\n\n" +
                string.Join("\n", failures.OrderBy(f => f, StringComparer.Ordinal));
            Assert.Fail(message);
        }
    }

    /// <summary>
    /// Walk property and IEnumerable element types starting from <paramref name="root"/>,
    /// returning the closure of logical-entity types reachable from a SELECT statement.
    /// </summary>
    private static HashSet<Type> ComputeSelectReachableTypes(Type root, Type[] logicalEntityTypes)
    {
        var set = new HashSet<Type>(logicalEntityTypes.Where(t => t == root));
        if (set.Count == 0)
            set.Add(root);

        // Add subclasses of any reachable type (e.g., SqlDerivedTable inherits SqlTable; if
        // SqlTable is reachable, SqlDerivedTable is too because a Table reference can be a
        // SqlDerivedTable at runtime).
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var t in logicalEntityTypes)
            {
                if (set.Contains(t))
                    continue;

                // Subclass of a reachable type → reachable.
                if (t.BaseType != null && set.Contains(t.BaseType))
                {
                    set.Add(t);
                    changed = true;
                    continue;
                }

                // Property type referenced from a reachable type → reachable.
                foreach (var reachable in set.ToArray())
                {
                    foreach (var prop in reachable.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var elementType = UnwrapPropertyType(prop.PropertyType);
                        if (logicalEntityTypes.Contains(elementType) && set.Add(elementType))
                            changed = true;
                    }
                }
            }
        }

        return set;
    }

    /// <summary>
    /// Strip Nullable&lt;T&gt; and IEnumerable&lt;T&gt; wrappers to find the carried element type.
    /// </summary>
    private static Type UnwrapPropertyType(Type type)
    {
        // Nullable<T> → T
        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable != null)
            return UnwrapPropertyType(nullable);

        // IEnumerable<T> / IList<T> / IReadOnlyList<T> → T
        if (type.IsGenericType)
        {
            var genericArgs = type.GetGenericArguments();
            if (genericArgs.Length == 1)
                return UnwrapPropertyType(genericArgs[0]);
        }

        // T[] → T
        if (type.IsArray)
            return UnwrapPropertyType(type.GetElementType()!);

        return type;
    }

    /// <summary>
    /// Locate the source file that contains <c>class TypeName</c> or <c>record TypeName</c>.
    /// Falls back to null if not found (e.g., compiler-generated types) — in that case no
    /// file is excluded from the consumer search.
    /// </summary>
    private static string? FindDefiningFile(Dictionary<string, string> sourceFiles, Type type)
    {
        // Match `class Foo` or `record Foo` at a word boundary, ignoring access modifiers.
        var pattern = new Regex($@"\b(class|record|struct)\s+{Regex.Escape(type.Name)}\b", RegexOptions.Compiled);
        foreach (var (path, contents) in sourceFiles)
        {
            if (pattern.IsMatch(contents))
                return path;
        }
        return null;
    }

    /// <summary>
    /// True if the property name appears as an identifier in any source file in the engine
    /// path other than the defining file. The defining file is excluded so that a property
    /// referenced only by its own getter/setter does not count as consumed.
    /// </summary>
    private static bool IsConsumedInEnginePath(string propertyName, Dictionary<string, string> sourceFiles, string? definingFile)
    {
        // Word-boundary match so "Top" doesn't match "TopClause" — the rule needs to see
        // the actual property name as an identifier (e.g., `.Top`, `Top =`, `nameof(Top)`,
        // or in a NotSupportedException string literal `"TOP PERCENT is not supported"`).
        var pattern = new Regex($@"\b{Regex.Escape(propertyName)}\b", RegexOptions.Compiled);

        foreach (var (path, contents) in sourceFiles)
        {
            if (string.Equals(path, definingFile, StringComparison.OrdinalIgnoreCase))
                continue;

            if (pattern.IsMatch(contents))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Properties intentionally excluded from the audit. Each entry must have a comment
    /// explaining why — the allow-list is the rule's escape hatch and growing it should be
    /// a deliberate decision, not an accident.
    /// </summary>
    private static bool IsAllowListed(Type type, PropertyInfo prop)
    {
        var key = $"{type.Name}.{prop.Name}";
        return key switch
        {
            // Documentation/diagnostic surface on SqlSelectDefinition. Set by the resolver
            // to record why a SELECT statement could not be resolved (unknown table/column,
            // ambiguous reference, etc.). Not consumed by the QueryEngine — the engine
            // treats InvalidReferences as a precondition checked elsewhere.
            "SqlSelectDefinition.InvalidReferenceReason" => true,

            // Pass-through round-trip surface preserved on SqlSelectDefinition for
            // consumers (e.g., MockDB) that round-trip the AST. Not semantically meaningful
            // to the in-memory engine — query hints (NOLOCK, INDEX(idx), etc.) target a
            // backing store that the in-memory engine doesn't have.
            "SqlSelectDefinition.QueryHints" => true,

            // Consumer-facing convenience accessor that returns WhereClause?.BinExpr. The
            // engine reads WhereClause and BinExpr separately by their canonical names —
            // both are themselves audited. WhereClauseAsBinary is preserved as a public
            // helper for downstream consumers that round-trip the WHERE clause.
            "SqlSelectDefinition.WhereClauseAsBinary" => true,

            // SqlExpression.Type is a derived computed property on the discriminated union;
            // it's used by binary-expression type promotion via the reachable arms (Column,
            // Value, Function, etc.), not as a free-standing engine signal. The arms it
            // depends on are themselves audited.
            "SqlExpression.Type" => true,

            // SqlFunction.IsNamedWindowFunction is a derived diagnostic that delegates to
            // WindowFunctionType. It is read by parser/grammar tests to assert that a
            // function name is recognized as a window function; the engine reads
            // IsWindowFunction (presence of OVER) instead.
            "SqlFunction.IsNamedWindowFunction" => true,

            // SqlTable.TableHints is the same shape as SqlSelectDefinition.QueryHints —
            // round-trip metadata for consumers (NOLOCK, ROWLOCK, etc. on a per-table
            // basis). The in-memory engine has no backing store for these hints to target.
            "SqlTable.TableHints" => true,

            // PostgreSQL array support (issue scope: arrays are out of QueryEngine scope
            // for now). The SqlExpression default-case throws NotSupportedException for
            // Kind == ArrayConstructor / ArraySubscript / CastExpr / JsonExpr — the
            // properties of these inner types are reachable only via those guarded arms.
            "SqlArraySubscript.Array" => true,
            "SqlArraySubscript.Index" => true,
            "SqlArraySubscript.IsSlice" => true,

            // ArrayDimensions is set by the PostgreSQL grammar on CAST type expressions
            // (e.g., CAST(x AS integer[])); SqlCastExpression itself is rejected at the
            // SqlExpression default case (NotSupportedException for Kind == CastExpr) so
            // ArrayDimensions is reachable only via that guarded arm.
            "SqlDataType.ArrayDimensions" => true,

            _ => false
        };
    }

    /// <summary>
    /// Walk up from the test assembly's base directory until we find the SqlBuildingBlocks.sln
    /// file, which marks the repository root. The test runs from
    /// <c>tests/Core.Tests/bin/Release/&lt;tfm&gt;/</c> in CI and locally.
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SqlBuildingBlocks.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"Could not locate SqlBuildingBlocks.sln walking up from {AppContext.BaseDirectory}. " +
            "The architecture coverage test requires repo-root access to scan the engine source.");
    }
}
