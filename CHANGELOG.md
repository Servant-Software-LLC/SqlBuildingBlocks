# Changelog

All notable changes to SqlBuildingBlocks are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions follow the CI-driven scheme `1.0.0.<run-number>`; every push to `main`
publishes all five NuGet packages
(`SqlBuildingBlocks.Core`, `SqlBuildingBlocks.Grammars.{AnsiSQL,MySQL,PostgreSQL,SQLServer}`)
at the same version.

---

## 1.0.0.286 — 2026-05-09 — Engine-completeness milestone

This release consolidates two `/uber-report` sessions (2026-05-05 Waves 1–5
and 2026-05-09 Waves 6–9) into a single stability-and-completeness
milestone. Test count grew from 706 (pre-uber-report) to **1197 passing**
across all projects; build is enforced at zero warnings with
nullable-as-error repo-wide.

### Added — Public API

- **`SqlBuildingBlocks.LogicalEntities.SqlExpressionKind`** (enum, 14 values).
  Discriminates the 14 mutually-exclusive arms of `SqlExpression`. Set once
  at construction and exposed via `SqlExpression.Kind`. Decided in
  [`Docs/Architectural/sqlexpression-shape-decision.md`](Docs/Architectural/sqlexpression-shape-decision.md);
  consumer impact analyzed in
  [`Docs/Architectural/sqlexpression-consumer-audit.md`](Docs/Architectural/sqlexpression-consumer-audit.md).
  All 14 existing public arm getters preserved unchanged — this is purely additive.
- **`SqlBuildingBlocks.LogicalEntities.IntervalLiteral`** (sealed class).
  Represents SQL:2003 single-field interval literals with `Magnitude` and
  `IntervalQualifier` (`Year`, `Month`, `Day`, `Hour`, `Minute`, `Second`).
  Used for window-frame `RANGE` bounds.
- **`SqlBuildingBlocks.LogicalEntities.SqlWindowFrameBoundOffset`** (sealed class)
  and **`SqlWindowFrameBoundOffsetKind`** (enum). Discriminated union
  representing either a numeric row count or an `IntervalLiteral`. Used by
  `SqlWindowFrameBound.Offset`. Decided in
  [`Docs/Architectural/window-frame-interval-decision.md`](Docs/Architectural/window-frame-interval-decision.md).
- **`SqlBuildingBlocks.Exceptions.SqlExecutionException`** (`Exception` subclass,
  `[Serializable]`). Typed exception for runtime/data-shape errors thrown by
  `QueryEngine`. Replaces previous bare `throw new Exception(...)` sites.

### Added — Engine features

- **`WITH` (non-recursive) CTE execution** (#125 closed in 2026-05-05;
  follow-up #174 still open for end-to-end parse-pipeline binding gap).
- **`UNION` / `INTERSECT` / `EXCEPT` set operations** (#124).
- **Window function execution**: `ROW_NUMBER`, `RANK`, `DENSE_RANK`, `LAG`,
  `LEAD`, `NTILE`, `FIRST_VALUE`, `LAST_VALUE`, `NTH_VALUE`. Frame defaults
  honored (`UNBOUNDED PRECEDING TO CURRENT ROW` when `ORDER BY` is present;
  full partition otherwise). `ROWS BETWEEN` and (engine-side) `RANGE
  BETWEEN INTERVAL ...` frames supported. Grammar-side `RANGE INTERVAL`
  parsing is tracked under #180.
- **`CAST(expr AS type)`** expression support (#20).
- **SQL Server `TOP N`** is honored at result-set materialization. `TOP N
  PERCENT` and `TOP N WITH TIES` raise `NotSupportedException` with a clear
  message naming the unsupported variant.
- **SQL:2003 §7.11 enforcement**: window functions in `WHERE`, `HAVING`,
  and `JOIN ON` predicates are rejected with `SqlExecutionException`.
  Window functions inside scalar subqueries are still permitted (the
  validator does not recurse into nested SELECT bodies).
- **`LAG()` / `LEAD()` argument validation**: zero-argument calls raise
  `NotSupportedException` instead of crashing with `IndexOutOfRangeException`.

### Added — Grammar correctness

- **`FROM` and `INTO` are now reserved words** in the AnsiSQL grammar.
  Before: `SELECT a, b, FROM Customers` parsed as a 3-column list
  `[a, b, FROM AS Customers]` because `IdentifierTerminal` accepted `FROM`
  in identifier position. Now: parser correctly rejects the dangling-comma
  form across all four dialects (#167). Inherited by MySQL, PostgreSQL,
  SQL Server.

### Added — Quality infrastructure

- **`tests/IntegrationTests/SqlBuildingBlocks.IntegrationTests`** — synthetic
  `ITableDataProvider` end-to-end test project (parse → resolve → execute
  round-trips). 16 passing scenarios, 3 skipped pending #168/#174.
- **`tests/Core.Tests/RoundTripTests.cs`** — `parse → render → re-parse → AST
  equality` safety net for AGENTS.md P0 "Incorrect AST round-trip." Full
  oracle for expressions; degraded oracle for statements (no
  statement-level renderer yet).
- **`benchmarks/SqlBuildingBlocks.Benchmarks`** project — BenchmarkDotNet
  baselines committed at `Docs/Benchmarks/baseline-2026-05-09-*.json`.
- **`<WarningsAsErrors>nullable</WarningsAsErrors>`** enforced repo-wide.
  Build fails on any CS86xx introduction.
- **CI guard**: `global.json` SDK version is asserted to match
  `Directory.Build.props` `NetTFM` major.minor on every push, preventing the
  recurrence of the `#134` SDK/TFM mismatch class.
- **Negative-SQL test corpus**: ≥86 asserting cases per dialect (was 0
  before this milestone). Audit documented in
  [`Docs/Audit/silent-accept-corpus-2026.md`](Docs/Audit/silent-accept-corpus-2026.md).

### Added — Architectural decision records

- [Recursive CTE depth-limit semantics](Docs/Architectural/recursive-cte-semantics-decision.md)
  — gates #168 implementation; chooses depth-limit-mandatory (default 100,
  matching SQL Server's `MAXRECURSION`), cycle detection deferred to
  future SQL:2016 `CYCLE` clause grammar work.
- [SqlExpression shape decision (Option C)](Docs/Architectural/sqlexpression-shape-decision.md)
  — Kind enum + invariant assertions, preserves all 14 public arm getters.
- [Window-frame INTERVAL bound modeling](Docs/Architectural/window-frame-interval-decision.md)
  — Option-C analog for `SqlWindowFrameBoundOffset`; consumer audit confirmed
  zero downstream touches before the type signature change.
- [SqlExpression consumer-pattern audit](Docs/Architectural/sqlexpression-consumer-audit.md)
  — verified FileBased.DataProviders has 3 narrow touches; MockDB and
  SettingsOnADO have zero touches. Drove Option C selection.

### Changed — Public API

- **`SqlBuildingBlocks.LogicalEntities.SqlWindowFrameBound.Offset`** is
  retyped from `int?` to `SqlWindowFrameBoundOffset?`. **This is a binary-
  compat break** but consumer audit (FileBased.DataProviders, MockDB,
  SettingsOnADO) confirmed zero downstream touches at type-signature
  level. Existing `(WindowFrameBoundType, int? = null)` constructor
  preserved; new `(WindowFrameBoundType, IntervalLiteral)` overload added.
- **`SqlExpression`** now asserts an invariant at construction and on every
  mutation through `AssumeExpressionLikeness`. Constructing or rewriting a
  `SqlExpression` with multiple arms set, or a `Kind` that disagrees with
  the populated arm, throws `InvalidOperationException`. Single-arm
  construction (the only legal pattern) is unchanged.
- **`QueryEngine`** `throw new Exception(...)` sites replaced with typed
  `SqlExecutionException`. Consumers catching the bare `Exception` continue
  to work; consumers that want narrower handling can now catch the typed
  exception.

### Fixed

- **#167** Parser silently accepts dangling SELECT comma (P0). Root cause
  was lexer keyword bleed-through, not LR(1) ambiguity. One-line fix:
  `grammar.MarkReservedWords("FROM", "INTO");`.
- **#173** SQL Server `TOP N` parses but engine ignores it. `Top` was only
  read at the CTE-clone propagation site; now honored at result emission.
- **#136** `SqlLiteralValueColumn` — investigation showed the class is
  fully wired and exercised by tests; the misleading TODO comment that
  declared it a placeholder was replaced with accurate XML doc.
- **#159** 17 `CS86xx` nullability warnings escaped `TreatWarningsAsErrors`
  on netstandard2.0. All sites fixed; explicit `<WarningsAsErrors>nullable</WarningsAsErrors>`
  added to ensure CI fails on any new occurrence.

### Tests

- 706 → 1197 passing (+491 tests this milestone).
- 6 skipped tests, all gated by tracked open issues (`#168`, `#174`).
- Build: 0 warnings, 0 errors. CI: Build + CodeQL both green on every wave.

### Known gaps (remaining open issues)

| Issue | Title | Status |
|-------|-------|--------|
| [#129](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/129) | QueryEngine reflection dispatch refactor | Deferred. Wave 9 baseline shows `ExecuteJoinedSelect` is 185× slower than simple SELECT and allocates 13× more — strong evidence the reflection cost is real. The refactor is now actionable with measurement data. |
| [#168](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/168) | Recursive CTE execution missing | Engine still single-shots the recursive term. Gated by ADR (#178, closed) and #174. 3 tests skipped pending. |
| [#174](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/174) | Parsed CTEs fail end-to-end | `SelectReferenceResolver` has zero CTE awareness; the resolver, not `CteTableDataProvider`, is the fix surface. Hand-built CTE QueryEngineTests pass; parse-pipeline tests skip. |
| [#177](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/177) | Architectural rule: every property must have engine consumer or NotSupported raise | Codification of the recurring smell behind #170/#173. |
| [#180](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/180) | Grammar-side INTERVAL frame parsing | Engine layer modeled; grammar layer still rejects `RANGE BETWEEN INTERVAL ...`. |
| [#181](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/181) | CI benchmarking workflow | Baseline captured; CI integration deferred. |

### Migration notes

- **`SqlWindowFrameBound.Offset`**: if you constructed `SqlWindowFrameBound`
  via the `(WindowFrameBoundType, int?)` constructor, no source change is
  required — that constructor is preserved. If you read `Offset` directly
  expecting `int?`, update to `Offset?.RowCount` (returns `int?`) for
  numeric bounds, or check `Offset?.Kind` to dispatch on numeric vs interval.
- **`SqlExecutionException`**: if you previously caught `Exception` to
  handle QueryEngine runtime errors, that continues to work. Consider
  narrowing to `SqlExecutionException` for the four runtime/data-shape
  error sites in `QueryEngine` (empty join list invariant, unresolved
  function in SELECT, unknown column type in projection, duplicate-name
  disambiguation).
- **`SqlExpression`**: no migration required. All 14 public arm getters
  retain identical signatures and semantics. The new `Kind` getter is
  additive; consumers that don't read it see no change.

---

## Pre-1.0.0.286 history

This is the first published changelog entry. Prior NuGet versions
(`1.0.0.<N>` for `N < 286`) represent intermediate snapshots produced by
CI on individual merges to `main`. The cumulative content of those
snapshots is summarized in the 1.0.0.286 entry above.

For prior commit-level history, see the
[git log](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/commits/main)
and the wave summaries under
[`.claude/tasks/`](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/tree/main/.claude/tasks).
