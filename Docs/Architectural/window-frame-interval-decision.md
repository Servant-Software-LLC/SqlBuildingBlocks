# `SqlWindowFrameBound.Offset` — INTERVAL Bound Modeling Decision

**Issue**: [#170](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/170) — _RANGE-mode window frames with INTERVAL bounds not modeled_
**Date**: 2026-05-09
**Wave**: /uber-report 2026-05-09 Wave 8 (Lane B)
**Decision**: **Option C analog** — discriminated `SqlWindowFrameBoundOffset` mirroring the Wave 4
[`SqlExpression` shape decision](sqlexpression-shape-decision.md).

## Context

`src/Core/LogicalEntities/SqlWindowSpecification.cs` modelled the offset of a `PRECEDING` /
`FOLLOWING` frame bound as `int? Offset` on `SqlWindowFrameBound`. That works for ROWS-mode frames
where the offset is a row count, but RANGE-mode frames in SQL:2003 admit offsets in the column's
domain — for time-series data, `RANGE BETWEEN INTERVAL '1' DAY PRECEDING AND CURRENT ROW`. There is
no way to attach an interval magnitude + qualifier to a frame bound; the engine treats every offset
as a row count regardless of `WindowFrameMode`.

## Audit findings (Phase 1, prerequisite for design)

Searched all three downstream consumer repositories under `C:\Dev\` for direct touches on
`SqlWindowFrameBound`, `SqlWindowFrame`, `SqlWindowSpecification`, and `IntervalLiteral`:

- **FileBased.DataProviders** (`C:\Dev\FileBased.DataProviders\src`): zero touches.
- **MockDB** (`C:\Dev\MockDB\src`): zero touches on the window types. Two SQL strings contain the
  literal word `INTERVAL` inside MySQL test data (`Sql89-GoldParser.txt`, `ROUTINES.Data.cs`); both
  are content, not API references.
- **SettingsOnADO** (`C:\Dev\SettingsOnADO\src`): zero touches.

**Public-API impact statement**: no consumer reads `SqlWindowFrameBound.Offset` directly.
Changing its type from `int?` to `SqlWindowFrameBoundOffset?` is binary-breaking only against
hypothetical future consumers; the audited surface is unaffected. **No `[Obsolete]` shim is required.**

The only in-repo callers of the `Offset` property today are:
- `src/Core/QueryProcessing/QueryEngine.cs` line 1797/1799 (the `GetFrameBoundIndex` switch). Updated.
- `tests/Core.Tests/SelectStmtTests.cs` lines 1092/1095 (numeric-offset assertions). Updated to
  read `Offset!.RowCount` instead of `Offset` directly.
- `src/Core/SelectStmt.cs` lines 754/755 (numeric-offset construction). Updated to wrap the int
  in `new SqlWindowFrameBoundOffset(int)`.

## Alternatives considered

### Option A — sealed subtype of `SqlWindowFrameBound` itself

Replace `SqlWindowFrameBound` with an abstract base + `NumericOffsetBound` / `IntervalOffsetBound`
sealed subtypes. Strongest typing.

**Rejected**: breaks the existing single public constructor `SqlWindowFrameBound(WindowFrameBoundType, int? = null)`,
breaks the `ToString()` switch on `Type` (which is currently the public discriminator), and forces
all current call sites in `SelectStmt.CreateWindowFrameBound` to dispatch to a different concrete
type. Disproportionate vs. Option C, which preserves the constructor surface and the type-based
ToString contract.

### Option B — sibling `IntervalOffset` property next to `Offset`

Add `public IntervalLiteral? IntervalOffset { get; }` next to the existing `int? Offset`. Either
one or the other is set for `Preceding` / `Following` bounds.

**Rejected**: this is exactly the "two of these are nullable, exactly one must be set" invariant
that the Wave 4 `SqlExpression` discriminated-union enforcement was created to eliminate. We just
solved this anti-pattern; we do not reintroduce it. Option C applies the same discipline at the
offset level.

### Option C analog — discriminated `SqlWindowFrameBoundOffset` (CHOSEN)

- New public class `SqlWindowFrameBoundOffset` with:
  - `public SqlWindowFrameBoundOffsetKind Kind { get; }`
  - `public int? RowCount { get; }`
  - `public IntervalLiteral? Interval { get; }`
  - Two public constructors: `(int rowCount)` and `(IntervalLiteral interval)`.
  - `private void AssertInvariant()` enforcing exactly-one-arm-non-null and arm matches Kind,
    called at the end of every constructor.
- New public enum `SqlWindowFrameBoundOffsetKind { Numeric, Interval }`.
- New public class `IntervalLiteral` with `(long Magnitude, IntervalQualifier Qualifier)`.
- New public enum `IntervalQualifier { Year, Month, Day, Hour, Minute, Second }`
  (SQL:2003 single-field qualifiers).
- `SqlWindowFrameBound.Offset` retyped from `int?` to `SqlWindowFrameBoundOffset?`.
  The existing `(WindowFrameBoundType, int? = null)` constructor accepts an `int?` overload that
  internally wraps in a numeric offset; a second `(WindowFrameBoundType, IntervalLiteral)`
  constructor was added.

**Why Option C analog won**: zero churn for current consumers (audit confirms no external touch),
mirrors the language already established in Wave 4 for discriminated unions, and admits the new
shape without polluting the existing one. Future bound shapes (e.g., `Groups`-mode-specific
extensions, expressions instead of literals) extend `SqlWindowFrameBoundOffsetKind` plus add a new
arm plus an invariant clause — exactly the rule the codebase already follows for `SqlExpression`.

## Future-evolution rule

Every new offset shape added to `SqlWindowFrameBoundOffset` MUST:

1. Add a value to `SqlWindowFrameBoundOffsetKind`.
2. Add a single-arm constructor that assigns the new arm AND `Kind` AND calls `AssertInvariant()`.
3. Extend `AssertInvariant()` to count the new arm.
4. Extend `ToString()` for diagnostic output.
5. Extend `QueryEngine.GetFrameBoundIndex` to interpret the new shape (or document why it is not
   supported by the engine).

## Engine semantics for INTERVAL bounds

`GetFrameBoundIndex` previously returned a row-index by adding/subtracting the numeric `Offset`
from the current row index. INTERVAL bounds compute differently — they are domain offsets, not
positional offsets. The implementation now:

- For numeric offsets: unchanged. `Preceding` returns `currentIndex - n`, `Following` returns
  `currentIndex + n`.
- For interval offsets: the engine computes a boundary value in the ORDER BY column's domain
  (`current.OrderByValue ± interval`), then walks the sorted partition to find the first/last row
  whose ORDER BY value falls within the closed range `[boundaryValue, currentValue]` (PRECEDING)
  or `[currentValue, boundaryValue]` (FOLLOWING). Requires exactly one ORDER BY column on the
  window spec and a `DateTime`-like type (we extend the qualifier table over time).

## Grammar gating (FINDING)

The engine now models INTERVAL frame bounds, but the grammar does not yet parse
`RANGE BETWEEN INTERVAL '1' DAY PRECEDING AND CURRENT ROW` into the new types. The un-skipped test
constructs the `SqlSelectDefinition` and `SqlWindowSpecification` programmatically by hand. A
follow-up grammar issue is filed in the Wave 8 report to extend `SelectStmt.CreateWindowFrameBound`
to recognize the `INTERVAL` literal-ish construct and emit `SqlWindowFrameBoundOffset` with an
`IntervalLiteral` arm.

## Files changed

- `src/Core/LogicalEntities/IntervalLiteral.cs` (new — public class + `IntervalQualifier` enum).
- `src/Core/LogicalEntities/SqlWindowFrameBoundOffset.cs` (new — discriminated offset + Kind enum).
- `src/Core/LogicalEntities/SqlWindowSpecification.cs` (`SqlWindowFrameBound.Offset` retyped;
  second constructor added for INTERVAL bounds).
- `src/Core/SelectStmt.cs` (numeric-offset construction wraps int in `SqlWindowFrameBoundOffset`).
- `src/Core/QueryProcessing/QueryEngine.cs` (`GetFrameBoundIndex` honors INTERVAL bounds with a
  pair of helpers `ComputeIntervalRangeBoundIndex` and `AddInterval`).
- `tests/Core.Tests/QueryProcessing/QueryEngineTests.cs` (un-skipped INTERVAL test; programmatic
  build).
- `tests/Core.Tests/SelectStmtTests.cs` (numeric-offset assertions read `Offset!.RowCount`).
- `Docs/Architectural/window-frame-interval-decision.md` (this document).
