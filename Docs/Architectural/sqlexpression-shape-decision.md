# `SqlExpression` Discriminated-Union Shape — Decision Record

**Issue**: [#132](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/132) — _SqlExpression is a discriminated union without enforcement_
**Date**: 2026-05-05
**Wave**: /uber-report Wave 4 (solo lane)
**Decision**: **Option C — `Kind` enum + `private`-set arms + invariant assertions** (recommended by the
[consumer-pattern audit](sqlexpression-consumer-audit.md), which is the prerequisite for this issue
and remains the reference for all "what consumers actually use" questions).

## Context

`src/Core/LogicalEntities/SqlExpression.cs` modelled a 14-arm discriminated union as a class with
14 nullable properties — `BinExpr`, `BetweenExpr`, `CaseExpr`, `CastExpr`, `ExistsExpr`, `Value`,
`Parameter`, `Column`, `Function`, `JsonExpr`, `ScalarSubqueryExpr`, `InList`, `ArrayConstructor`,
`ArraySubscript` — with a comment on line 26 reading _"Logic within this class should enforce that
only one of these properties is ever set"_ and no enforcement code anywhere. Construction was via
14 single-arm constructors; mutation was via the private `AssumeExpressionLikeness` rewrite path
called from the visitor leaf-handler. Every getter consumer (the LINQ `GetExpression()` ladder,
`ToExpressionString`, `ToString`, `Accept`) walked a chain of null checks in a precedence-baked-in
order.

The `AssumeExpressionLikeness` rewrite path nulled all 14 arms and re-installed exactly one based
on which arm of the source `SqlExpression` was non-null — but did not assert the invariant on exit,
silently producing a no-arm-populated `SqlExpression` if the source was malformed.

## Audit findings (carried forward from #157)

The [consumer-pattern audit](sqlexpression-consumer-audit.md) catalogued every external consumer
and found:

- **MockDB**: zero touches on `SqlBuildingBlocks.LogicalEntities.SqlExpression`
- **SettingsOnADO**: zero touches
- **FileBased.DataProviders**: 3 enforcement-sensitive touch sites, all reading via
  `SqlAssignment` convenience properties (`Value`, `Parameter`, `Function`) which pass through to
  `Expression.Value/.Parameter/.Function`. Two more sites only read `ToExpressionString()`.
  No consumer reads any of the other 11 arms directly.

## Alternatives considered

### Option A — sealed subtypes + pattern matching

Replace `SqlExpression` with a sealed abstract base + 14 sealed subclasses; consumers
pattern-match: `if (assignment.Expression is SqlValueExpression v) { ... }`. Cleanest typing,
strongest enforcement.

**Rejected**: major binary break. `SqlExpression` is widely consumed as a constructable type
with a public getter surface. Removing the property surface would force every consumer
(including all three audited downstream repos and three SqlAssignment convenience constructors)
to migrate. Disproportionate for a 3-touch consumer surface that Option C can serve without any
break.

### Option B — case-discriminator enum + `Payload` object

Add `public SqlExpressionKind Kind { get; }` and a single `object Payload { get; }`; mark all 14
arm properties `[Obsolete]` shims that read through `Kind` + `Payload`.

**Rejected**: doubles the surface during deprecation; hides the union shape behind ceremony;
reintroduces casts. Workable but cumbersome and offers no enforcement gain over Option C.

### Option C — `Kind` enum + `private`-set arms + invariant assertions (CHOSEN)

- Introduce `public enum SqlExpressionKind` with one value per arm.
- Add `public SqlExpressionKind Kind { get; private set; }`, set once at construction time
  (NOT lazily derived from the property ladder).
- Keep all 14 nullable getter properties intact; `private set` was already in place per the audit.
- Add a single `private void AssertInvariant()` that throws `InvalidOperationException` if the
  invariant breaks (count of populated arms ≠ 1 OR populated arm ≠ Kind).
- Call `AssertInvariant()` at the end of every public constructor and at the end of
  `AssumeExpressionLikeness`.
- Refactor `GetExpression()` to switch on `Kind` so the precedence becomes explicit.

**Why Option C won**: zero binary break for the audited consumer surface. `FileUpdateWriter`,
`FileInsert`, and the convenience properties on `SqlAssignment` continue to compile and execute
unchanged. The only new public surface area is the `SqlExpressionKind` enum and the `Kind`
getter — both purely additive.

## Binary-compatibility strategy

| Surface | Before | After | Compat |
|---|---|---|---|
| 14 nullable arm getters | `public SqlXxx? Xxx { get; private set; }` | unchanged | ✅ source + binary |
| 14 single-arm constructors | `public SqlExpression(...)` | unchanged signature; now sets `Kind` and calls `AssertInvariant()` | ✅ source + binary |
| `GetExpression(...)` | null-check ladder | switch on `Kind`, identical precedence and return contracts | ✅ behavior |
| `ToExpressionString()`, `ToString()`, `Accept()` | unchanged | unchanged | ✅ behavior |
| `Type` property | unchanged | unchanged | ✅ |
| `AssumeExpressionLikeness` | private | private; now also mirrors `Kind` and asserts on exit | ✅ (private) |
| `SqlAssignment.Value/.Parameter/.Function` pass-throughs | unchanged | unchanged | ✅ |
| `Kind` getter | (none) | new public additive | ✅ additive |
| `SqlExpressionKind` enum | (none) | new public additive | ✅ additive |

No public type was deleted, renamed, or had its accessibility narrowed. No method or constructor
signature changed. Audited consumer code in FileBased.DataProviders compiles unchanged against
the new `SqlExpression`.

## Migration path

**None required for current consumers.** Existing consumer code that null-checks individual arm
properties (e.g. `if (assignment.Value != null)`) continues to work — the populated arm is still
non-null. Consumers that want explicit dispatch may now switch on `expression.Kind`, but no
existing code is forced to change.

## Future evolution rule

Every new arm added to `SqlExpression` MUST:

1. **Add a value to `SqlExpressionKind`.** Names mirror the property name on `SqlExpression`.
2. **Add a single-arm constructor** that assigns the new arm AND `Kind` AND calls
   `AssertInvariant()`.
3. **Extend `AssertInvariant()`** to count the new arm.
4. **Extend `AssumeExpressionLikeness`** to mirror the new arm + Kind together.
5. **Extend `GetExpression()`'s switch** with the new `Kind` case (or document a `default`-case
   reason for not supporting LINQ generation for the new arm).
6. **Extend `ToExpressionString()`, `ToString()`, and `Accept()`** to handle the new arm.

A failure to keep these in lockstep will be caught at construction time by `AssertInvariant()`,
which is the gain from this refactor: silent drift between Kind and arms is no longer possible.

## Findings

### `AssumeExpressionLikeness` was not silently lossy in practice

The audit warned that `AssumeExpressionLikeness` _"silently rewrites" properties_. On
re-inspection, the historical implementation already correctly nulled all 14 arms before
re-installing one of them. The genuine hazard was the missing post-condition: a malformed source
expression (no arm populated) would produce a no-arm `SqlExpression` instead of a diagnostic.
Option C closes that hazard with the new throw at the end of `AssumeExpressionLikeness`'s else
branch, plus the trailing `AssertInvariant()` call.

### Visitor return contract is preserved

`HandleLeafNode<T>` only invokes `AssumeExpressionLikeness` when the visitor returns a non-null
replacement, and visitors that don't intend to morph return `null`. Visitors that DO morph
construct the replacement via the public single-arm constructors, which are themselves invariant.
So `AssumeExpressionLikeness` always receives a well-formed source expression, and the throw
guards only against future bugs — not any current code path.

### The original null-check ladder embedded a precedence

`BinExpr → BetweenExpr → CaseExpr → Value → Function → Parameter → ScalarSubqueryExpr → Column`
was the historical evaluation order. The Kind-driven switch in the new `GetExpression()` has an
explicit case per arm in the same precedence; consumer-relied behavior on the `Value`,
`Parameter`, and `Function` arms is bit-identical.

## Sign-off

| Owner | Status | Date |
|---|---|---|
| FileBased.DataProviders | covered by audit; Option C sign-off recorded in #157 | 2026-05-05 |
| MockDB | N/A — no consumer touches | 2026-05-05 |
| SettingsOnADO | N/A — no consumer touches | 2026-05-05 |

## Files changed

- `src/Core/LogicalEntities/SqlExpressionKind.cs` (new — public enum)
- `src/Core/LogicalEntities/SqlExpression.cs` (Kind property; constructors set Kind and call
  AssertInvariant; AssertInvariant added; AssumeExpressionLikeness mirrors Kind and asserts on
  exit; GetExpression switches on Kind)
- `tests/Core.Tests/LogicalEntities/SqlExpressionTests.cs` (per-Kind construction tests, invariant
  violation tests via reflection, AssumeExpressionLikeness round-trip via ResolveParametersVisitor)
- `Docs/Architectural/sqlexpression-shape-decision.md` (this document)
