# Recursive CTE Semantics Decision

**Issue**: [#178](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/178) — gates [#168](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/168)
**Date**: 2026-05-09
**Author**: `/uber-report 2026-05-09` orchestrator (synthesizing Plan Reviewer + Devil's Advocate findings)
**Status**: Accepted

## Context

Issue #168 proposes implementing recursive CTE execution in `QueryEngine.ExecuteCte` (`src/Core/QueryProcessing/QueryEngine.cs:1731`). The issue's Acceptance Criteria reads:

> A configurable depth limit (e.g., 1000) prevents runaway recursion; runaway recursion throws a typed `SqlExecutionException`. Cycle detection or depth-bound termination is documented behavior.

The "OR" between cycle detection and depth-bound termination conflates two distinct semantics. Picking the wrong one — or shipping with both implicit — would lock consumers into behavior that diverges from every reference engine.

This ADR records the choice **before** implementation begins.

## Reference Engine Survey

| Engine | Default behavior | Configuration | Cycle detection |
|--------|------------------|---------------|-----------------|
| **PostgreSQL** | Iteration cap + `work_mem` budget | `cte_max_recursion_depth` is non-existent; PG does not have a documented depth knob. Runaway recursion is bounded by memory exhaustion, which is then converted to a planner error. | Not by default. SQL:2016 `CYCLE` clause (PostgreSQL 14+) provides explicit opt-in cycle marking. |
| **SQL Server** | Depth-limited | `OPTION (MAXRECURSION N)` query hint. Default 100. Hard ceiling 32767. `MAXRECURSION 0` disables the limit. | Not at all. SQL Server has never supported the SQL:2016 `CYCLE` clause. |
| **MySQL 8** | Depth-limited | `cte_max_recursion_depth` session variable. Default 1000. | Not by default. SQL:2016 `CYCLE` clause is unsupported. |
| **SQLite** | Iteration cap (LIMIT in the recursive query) | No dedicated knob; controlled by query-side `LIMIT`. | Not by default. |
| **Oracle** | Depth-limited | `CYCLE` clause supported (SQL:2016) plus an internal recursion budget. | Yes, with explicit `CYCLE` clause; not by default. |

**Convergent finding**: **No mainstream engine performs cycle detection by default.** Every engine relies on depth-bound termination as the primary safety mechanism, with cycle detection (where supported) opt-in via the SQL:2016 `CYCLE` clause.

## Decision

### Default safety mechanism: depth-bound termination

`QueryEngine.ExecuteCte` will iterate the recursive term until **either**:
1. The working set produced by the previous iteration is empty (natural termination), **or**
2. The cumulative iteration count reaches a configured depth limit (runaway termination).

When the depth limit fires, the engine throws `SqlBuildingBlocks.Exceptions.SqlExecutionException` with a message of the form:

> `Recursive CTE '<cte-name>' exceeded the maximum recursion depth of <N> iterations. This usually indicates an unbounded recursion or a cycle in the underlying data. Increase QueryEngineOptions.MaxRecursionDepth or add a termination predicate to the recursive term.`

The CTE name and the depth at which the limit fired must appear in the message. This is mandatory per the issue AC and matches the diagnostic shape SQL Server's `MAXRECURSION` raises.

### Default depth limit: **100**

Matches SQL Server's default. Lower than MySQL's 1000 — chosen on the grounds that 100 is "obvious safety net for almost any reasonable recursive query, surfaces unbounded recursion fast." Consumers who legitimately need deeper recursion can raise the knob.

### Configuration knob: `QueryEngineOptions.MaxRecursionDepth`

A new (or extended — verify whether `QueryEngineOptions` already exists) options class. The property is:

```csharp
public int MaxRecursionDepth { get; init; } = 100;
```

Validation: must be `>= 1`. A value of `0` or negative is rejected with `ArgumentOutOfRangeException` at construction time. There is **no** sentinel for "no limit" — consumers who want effectively unlimited recursion set a large finite value (e.g., `int.MaxValue`).

### Cycle detection: **out of scope for v1**

We will NOT implement cycle detection in the initial #168 work. Reasons:
1. No reference engine does it by default.
2. Implementation requires per-row identity tracking across iterations, which has cost and complexity that pays for nothing the depth limit doesn't already cover for the common case.
3. The SQL:2016 `CYCLE` clause is the standard surface for opting in. It is dialect-specific (PostgreSQL 14+, Oracle) and has its own grammar work. **Cycle detection should track the `CYCLE` clause grammar work, not the depth-limit work**.

When the `CYCLE` clause is added in a future PR (likely against the PostgreSQL grammar first), cycle detection becomes opt-in: consumers who write `WITH RECURSIVE … CYCLE col SET is_cycle USING path` get cycle marking; everyone else gets the depth limit only. This matches PostgreSQL's behavior exactly.

### What about the case where the recursion is unbounded but doesn't cycle?

The depth limit catches it. Example: `WITH RECURSIVE counter AS (SELECT 1 AS n UNION ALL SELECT n+1 FROM counter) SELECT * FROM counter` — no cycle, but unbounded. After 100 iterations the engine throws. Consumers see a clear error naming the depth limit and the CTE name.

### What about UNION (deduplicating) vs UNION ALL semantics?

The recursive-term combinator in standard SQL is `UNION ALL` (each iteration's rows are appended). `UNION` (deduplicating) is supported by SQL Server but is rare. **#168 v1 will support `UNION ALL` only**; `UNION` in a recursive term raises `NotSupportedException` with a clear message. Filed as a follow-up issue if a consumer needs it.

## Implementation Notes for #168

These are guidance for the implementing agent, not part of the ADR's normative content:

1. **Iteration loop**: maintain a working set (the rows produced by the most recent iteration) and an accumulating result set. Each iteration evaluates the recursive term against the working set, appends new rows to the result, and replaces the working set with the new rows. Terminate when the new rows are empty or when the depth counter exceeds `MaxRecursionDepth`.

2. **`CteTableDataProvider` semantics**: the existing implementation overwrites a single `DataTable` per CTE name. Recursive iteration needs append-or-replace: append new rows to the cumulative result, replace the working-set view that the recursive term sees. Two distinct collections, both keyed by the same CTE name.

3. **Anchor vs recursive term identification**: a CTE is recursive when `SqlCteDefinition.IsRecursive` is true AND the SELECT body contains a `UNION ALL` (or `UNION`) where the recursive arm references the CTE name. The anchor is the side that does NOT reference the CTE. Validate this at execution time; raise `SqlExecutionException` if the structure doesn't match.

4. **Interaction with #174 (CTE binding gap)**: the recursive term references the CTE by name. The `SelectReferenceResolver` must be able to resolve column references in the recursive term against the CTE's projected columns. **#174 must land before #168 implementation** — see the plan-review comment on #174 for the resolver-gap analysis.

5. **Test coverage** (already scaffolded in `tests/Core.Tests/QueryProcessing/QueryEngineTests.cs`):
   - `Query_RecursiveCte_HierarchyTraversal_TraversesAllLevels` — happy path
   - `Query_RecursiveCte_BoundedRecursion_TerminatesAtBound` — natural termination
   - `Query_RecursiveCte_RunawayRecursion_Throws` — depth-limit firing; the assertion message must match the format above
   - Plus the integration scenario in `tests/IntegrationTests/AnsiSqlScenarioTests.cs`
   - Add: configurable depth limit test (override `MaxRecursionDepth` to a low value, assert the limit-fire happens at that depth)

## Consequences

- **Predictable** — depth-limit is the single safety net. Consumers know exactly what to expect.
- **Aligned with industry** — matches SQL Server's default behavior, which is the most widely-used RDBMS in the .NET ecosystem.
- **Future-extensible** — when SQL:2016 `CYCLE` clause grammar work lands, cycle detection slots in as opt-in semantics without changing this ADR's defaults.
- **One trap** — consumers porting from databases without depth limits (older PostgreSQL) may hit the 100 limit unexpectedly on legitimate workloads. Mitigation: the error message names the knob to raise.

## Related work

- **#168** — recursive CTE execution. Implementation work; gated by this ADR.
- **#174** — CTE binding gap. Must land before #168.
- **Future** — SQL:2016 `CYCLE` clause grammar + cycle detection. Tracked as a follow-up when a consumer needs it.
- **Future** — `MAXRECURSION` query hint syntax (SQL Server-specific). Tracked when SQL Server grammar prioritizes it.

## Sign-off

| Role | Decision | Date |
|------|----------|------|
| `/uber-report 2026-05-09` Plan Reviewer | Recommend depth-limit + cycle-detection-deferred | 2026-05-09 |
| `/uber-report 2026-05-09` Devil's Advocate | Ratify | 2026-05-09 |
| `/uber-report 2026-05-09` Architect | Ratify | 2026-05-09 |
| Implementing agent (#168) | Must cite this ADR in PR description | _pending_ |
