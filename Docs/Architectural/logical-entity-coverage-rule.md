# Logical-Entity Coverage Rule

**Issue**: [#177](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/177)
**Date**: 2026-05-10
**Wave**: 15 (process-polish)

## Decision

Every public instance property on a logical-entity type that is reachable from
`SqlSelectDefinition` (the SELECT execution surface processed by `QueryEngine`)
must be backed by **either**:

1. A consumer in the engine path (`src/Core/QueryProcessing/`,
   `src/Core/Utils/`, `src/Core/Visitors/`, or a sibling logical-entity helper
   under `src/Core/LogicalEntities/`) that reads it, **OR**
2. An explicit `NotSupportedException` raise-site keyed off the unsupported
   feature — typically inside `QueryEngine.ThrowIfUnsupportedFeatures()`.

A property that is neither consumed nor guarded is "silently ignored" by the
engine. This is the recurring root cause behind multiple shipped bugs (see
"Recurring-bug inventory" below) and is no longer acceptable.

The rule is enforced by a reflection-based unit test:
[`tests/Core.Tests/Architecture/LogicalEntityCoverageTests.cs`](../../tests/Core.Tests/Architecture/LogicalEntityCoverageTests.cs).

## Rationale

### Recurring-bug inventory

Three live issues from earlier waves and several previously-resolved ones share
one architectural smell: **a logical-entity property is populated by the parser,
but the engine silently ignores it.**

| Issue | Symptom | Root cause |
|-------|---------|------------|
| [#173](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/173) (closed) | `SELECT TOP 5 ...` returned the full result set | `SqlSelectDefinition.Top` set by parser; engine never read it |
| [#170](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/170) (closed) | RANGE+INTERVAL window frames produced wrong rows | `SqlWindowFrameBound.Offset` modeled numeric only; engine treated every offset as a row count |
| [#174](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/174) (closed) | CTE column resolution failed silently | `CteTableDataProvider` rows produced without `TableRef` binding |

The fix for each was localized; the underlying pattern keeps recurring with new
features. Codifying the rule turns "the engine forgot to read this property"
from a runtime silent-wrong-result class of bug into a compile-time failure of
the architecture test.

### Why a reflection test, not a Roslyn analyzer

A Roslyn analyzer would provide compile-time enforcement at the cost of
maintenance overhead — the project already centralizes warning-as-error rules
in `Directory.Build.props` and adding an analyzer assembly is heavier than the
problem demands. The reflection test runs in CI, fails on the same pull
request that introduces a gap, and produces a human-readable list of the
offending properties. It is also testable in isolation (the test itself can be
unit-tested by adding a deliberate gap and asserting the failure message).

### Identifier-substring scan, not semantic analysis

The test does not parse C# semantically. It checks whether each property's name
appears as an identifier (word-boundary regex) in any `.cs` file under the
engine path, *excluding* the file that declares the property's type. This is
deliberately a **tripwire**, not a proof:

- **False positives are tolerated** — if the property name happens to appear in
  an unrelated comment or another type's member, the test reports "consumed"
  even though the engine doesn't actually read it. This is acceptable because
  the goal is to catch the recurring "silently ignored" pattern, not to prove
  every property is correctly executed.
- **False negatives are the failure mode the rule prevents** — a property
  whose name appears nowhere in the engine path is unambiguous evidence that
  the engine cannot have read it.

## Audit (Wave 15)

The audit at the time the rule was introduced (Wave 15, branch
`feature/wave-15-process-polish`) inventoried **123** declared public instance
properties across **34** SELECT-reachable logical-entity types (out of 82 total
logical-entity types in `SqlBuildingBlocks.LogicalEntities`). The audit
surfaced 8 candidate gaps:

| Property | Resolution |
|----------|------------|
| `SqlWindowFrame.Mode` | **Fixed inline** — added a `NotSupportedException` raise-site in `ThrowIfUnsupportedFeatures` for Mode = Groups and for Mode = Range with a numeric offset. The engine had been silently treating RANGE + numeric as ROWS-positional; #170 partially resolved this for the INTERVAL case but left Numeric unguarded. |
| `SqlSelectDefinition.QueryHints` | Allow-listed — round-trip metadata for downstream consumers (NOLOCK, INDEX(idx), etc.); the in-memory engine has no backing store for these hints to target. |
| `SqlSelectDefinition.InvalidReferenceReason` | Allow-listed — diagnostic property set by `SelectReferenceResolver`; the engine treats `InvalidReferences` as a precondition checked at higher layers. |
| `SqlSelectDefinition.WhereClauseAsBinary` | Allow-listed — consumer-facing convenience accessor; the engine reads the underlying `WhereClause.BinExpr` directly, both of which are themselves audited. |
| `SqlTable.TableHints` | Allow-listed — same shape as QueryHints (per-table NOLOCK, ROWLOCK, etc.); round-trip metadata for downstream consumers. |
| `SqlFunction.IsNamedWindowFunction` | Allow-listed — derived diagnostic; the engine reads `IsWindowFunction` (presence of OVER) instead. |
| `SqlArraySubscript.Array` / `Index` / `IsSlice` | Allow-listed — reachable only via `SqlExpression.Kind == ArraySubscript`, which is rejected at the SqlExpression default-case `NotSupportedException` raise-site. |
| `SqlDataType.ArrayDimensions` | Allow-listed — reachable only via `SqlExpression.Kind == CastExpr` (PostgreSQL `CAST(x AS integer[])`), which is rejected at the same default case. |

After the fix and allow-listing, the test passes with zero genuine gaps.

## Enforcement Seam

The single source of truth for "what is and is not supported by the engine" is
`QueryEngine.ThrowIfUnsupportedFeatures()` at the top of
[`src/Core/QueryProcessing/QueryEngine.cs`](../../src/Core/QueryProcessing/QueryEngine.cs).

This method is called once per `Query()` invocation, before any execution
work. Adding a new guard is a one-liner that throws a `NotSupportedException`
with a message that names the unsupported combination. The architecture test's
identifier-substring scan picks up the property name from the message string
literal, so a `throw new NotSupportedException("RANGE-mode window frames with
a numeric offset are not supported...")` simultaneously satisfies (a) the rule
and (b) the user-facing diagnostic.

## When the Rule Applies

The audit is scoped to types **reachable from `SqlSelectDefinition`** via
property/element-type traversal. This excludes:

- DML/DDL entities (`SqlInsertDefinition`, `SqlUpdateDefinition`,
  `SqlDeleteDefinition`, `SqlMergeDefinition`, `SqlCreate*Definition`,
  `SqlAlter*Definition`, `SqlDrop*Definition`, `SqlSavepointDefinition`,
  `SqlTransactionDefinition`, etc.).

These types are consumed by downstream packages (FileBased.DataProviders,
MockDB), not by the in-memory `QueryEngine`. Their coverage rules belong to
the consuming package's own architecture, not to SqlBuildingBlocks.

If a future SqlBuildingBlocks feature introduces an in-process executor for
DML statements (e.g., a hypothetical `InsertEngine`), the rule should expand
to types reachable from that executor's root and the test scope updated
accordingly.

## Adding a Property to a Logical Entity (Contributor Checklist)

When you add a new public property to a logical-entity type that is reachable
from `SqlSelectDefinition`:

1. **Wire engine consumption** if the property is meant to affect query
   execution. Read it from `QueryEngine` (or one of the engine-path helpers)
   in the appropriate code path.
2. **OR add a guard** in `ThrowIfUnsupportedFeatures()` if the engine does
   not yet honor the property. The guard should be a `NotSupportedException`
   with a message that names the unsupported combination.
3. **OR add the property to the allow-list** in
   `LogicalEntityCoverageTests.IsAllowListed` with a comment explaining why
   the property is not consumed (round-trip metadata, derived diagnostic, etc.).
4. **Run the architecture test** to confirm:
   `dotnet test --configuration Release --filter "FullyQualifiedName~LogicalEntityCoverageTests"`.

The architecture test failure message lists each offending property as
`TypeName.PropertyName` and points at this ADR for the rule.

## See Also

- [`sqlbuildingblocks-architecture-knowledge`](../../.claude/skills/sqlbuildingblocks-architecture-knowledge/SKILL.md)
  — codifies this rule alongside the other load-bearing invariants.
- [`sqlbuildingblocks-dev-knowledge`](../../.claude/skills/sqlbuildingblocks-dev-knowledge/SKILL.md)
  — contributor checklist mirrors the four-step list above.
- [`Docs/Architectural/sqlexpression-consumer-audit.md`](sqlexpression-consumer-audit.md)
  — sibling audit covering the discriminated-union shape of `SqlExpression`.
