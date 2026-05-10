# SqlBuildingBlocks Benchmarks

This directory holds the captured baseline output from
`benchmarks/SqlBuildingBlocks.Benchmarks` along with run/compare instructions and the
threshold rule used to evaluate regressions.

The benchmark project provides BenchmarkDotNet-driven measurements of the parser hot
paths and the QueryEngine execution path. It exists so future optimization work
(notably issue #129 — reflection-based generic dispatch in QueryEngine) has a
quantitative target instead of subjective claims.

## How to run

From the repository root:

```powershell
# Quick run with the ShortRun job (3 warmups, 3 iterations) — useful during local development.
dotnet run --configuration Release --project benchmarks/SqlBuildingBlocks.Benchmarks -- --filter "*" --job short

# Full statistical run (default job — slower, but produces stable confidence intervals).
dotnet run --configuration Release --project benchmarks/SqlBuildingBlocks.Benchmarks -- --filter "*"

# Single benchmark class.
dotnet run --configuration Release --project benchmarks/SqlBuildingBlocks.Benchmarks -- --filter "*ParseBenchmarks*"

# Emit JSON artifacts for diffing against a baseline.
dotnet run --configuration Release --project benchmarks/SqlBuildingBlocks.Benchmarks -- --filter "*" --exporters json
```

Artifacts land in
`benchmarks/SqlBuildingBlocks.Benchmarks/BenchmarkDotNet.Artifacts/results/` —
markdown summaries, CSV, HTML, and (with `--exporters json`) full JSON.

## How to compare against the baseline

BenchmarkDotNet can take a baseline JSON for delta reporting, but the simplest
workflow is the GitHub-style markdown report committed alongside this README:

1. Run a fresh benchmark with the same `--job` flag used for the baseline (`short`
   for the file dated 2026-05-09).
2. Open the markdown report in `benchmarks/SqlBuildingBlocks.Benchmarks/BenchmarkDotNet.Artifacts/results/*-report-github.md`
   and diff each row against the corresponding baseline file in this folder.
3. Apply the regression rule below.

For automated comparison, BenchmarkDotNet's `[BaselineColumn]` attribute on a method
in the same class produces `Ratio` columns. Cross-class baselines require an external
tool (the BenchmarkDotNet `--baseline` flag is for *job-vs-job* within a single run,
not against a stored baseline file).

## Regression threshold

A change to a benchmarked method should be flagged for review when, for the same
machine class:

- **Mean is more than 10 percent slower than the baseline**, OR
- **Allocated bytes are more than 20 percent higher than the baseline**.

Rationale: parse cost dominates downstream consumer latency in MockDB and
FileBased.DataProviders, and allocation churn surfaces as Gen0 GC pressure under
high QPS. The 10/20 split is conservative — actual targets will tighten once #129
lands.

## Baseline — 2026-05-09

All numbers below come from BenchmarkDotNet `--job short`. Treat them as
order-of-magnitude indicators with wide confidence intervals (3 iterations is too
few for sub-microsecond methods to stabilize) — full-job runs should replace these
once a regression-detection workflow exists.

### Machine

```
BenchmarkDotNet=v0.13.4, OS=Windows 11 (10.0.26200.8328)
11th Gen Intel Core i7-1185G7 3.00GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK=10.0.201
[Host] / ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
```

### Parser benchmarks

| Method                              | Mean       | Allocated  |
|------------------------------------ |-----------:|-----------:|
| ParseSimpleSelect_Ansi              |   4.40 us  |   ~7.6 KB  |
| ParseSimpleSelect_MySql             |   4.34 us  |    7.6 KB  |
| ParseComplexSelect_Ansi             |  59.08 us  |   65.1 KB  |
| ParseComplexSelect_MySql            |  62.01 us  |   65.8 KB  |
| ParseDeeplyNestedExpression_Ansi    |   3.71 us  |   ~8.5 KB  |
| ParseCte_Ansi                       |  13.15 us  |   18.1 KB  |
| ParseAndCreate_ComplexSelect_Ansi   | 166.77 us  |   82.6 KB  |

Source JSON: [`baseline-2026-05-09-ParseBenchmarks.json`](baseline-2026-05-09-ParseBenchmarks.json) and
[`baseline-2026-05-09-ParseBenchmarks.md`](baseline-2026-05-09-ParseBenchmarks.md).

### QueryEngine benchmarks (100-row tables)

| Method              | Mean        | Allocated   |
|-------------------- |------------:|------------:|
| ExecuteSimpleSelect |    1.94 ms  |   207.5 KB  |
| ExecuteJoinedSelect |  359.74 ms  |  2648.4 KB  |

Source JSON: [`baseline-2026-05-09-QueryEngineBenchmarks.json`](baseline-2026-05-09-QueryEngineBenchmarks.json) and
[`baseline-2026-05-09-QueryEngineBenchmarks.md`](baseline-2026-05-09-QueryEngineBenchmarks.md).

## Findings from the baseline run

These observations are starting points for follow-up performance work — they are
**not regressions**, they are existing costs the benchmarks now expose:

1. **`ExecuteJoinedSelect` is 185x slower than `ExecuteSimpleSelect` on the same row
   count (360 ms vs 1.94 ms for two 100-row tables).** Allocated bytes are also
   ~13x higher (2.65 MB vs 208 KB). This is the strongest signal that the
   reflection-based generic dispatch tracked in #129 is a real cost. A 100x100
   inner join evaluating in 360 ms implies roughly 36 us per row pair — the
   QueryEngine is doing meaningful per-row reflection work.
2. **`ParseAndCreate_ComplexSelect_Ansi` is ~2.8x slower than parse-only
   (`ParseComplexSelect_Ansi`).** Create() walking and NonTerminal dispatch costs
   nearly as much as the Irony parse itself for non-trivial SELECTs. If #129 also
   reduces Create-side reflection, this is the parse-side test for it.
3. **`ParseDeeplyNestedExpression_Ansi` is *faster* than `ParseSimpleSelect_Ansi`
   (3.7 us vs 4.4 us).** The nested expression has no FROM clause work because the
   benchmark prepends `SELECT * FROM T WHERE ...`; left-recursive binary expressions
   parse extremely efficiently in Irony. This means a 50-deep nested expression is
   not a stress case for parse perf — it's a stress case for stack depth (verified
   non-fatal by Wave 2). For perf, add wider-shape stress (long IN lists, wide
   SELECT lists) when #129 work begins.

## Post-Wave-12 baseline — 2026-05-09 (#129 resolved)

Wave 12 of `/uber-report 2026-05-09` replaced the QueryEngine reflection dispatch
(`ReflectionHelper.CallMethod` -> `MethodInfo.Invoke`) with cached compiled-delegate
dispatch in `SqlBuildingBlocks.Utils.CompiledQueryDispatch`. The baseline below
captures the post-refactor numbers on the same machine and `--job short` config.

### QueryEngine benchmarks (100-row tables) — post-Wave-12

| Method              | Mean        | Allocated    | vs original baseline |
|-------------------- |------------:|-------------:|---------------------:|
| ExecuteSimpleSelect |   808.8 us  |    207.3 KB  | 2.4x faster, equal alloc |
| ExecuteJoinedSelect |   240.4 ms  |   2634.5 KB  | 1.5x faster, ~equal alloc |

Source JSON: [`baseline-2026-05-09-wave12-QueryEngineBenchmarks.json`](baseline-2026-05-09-wave12-QueryEngineBenchmarks.json) and
[`baseline-2026-05-09-wave12-QueryEngineBenchmarks.md`](baseline-2026-05-09-wave12-QueryEngineBenchmarks.md).

## Post-Wave-14 baseline — 2026-05-10 (#188 resolved)

Wave 14 lane D of `/uber-report 2026-05-10` added the per-(predicate-shape, TDataRow,
tableDataRow) compiled-delegate cache in `SqlBuildingBlocks.Utils.CompiledPredicateCache`.
The cached lambda accepts the substitute-values dictionary as a runtime parameter
(rather than baking constants per call) and the dispatch site invokes it via
`Enumerable.Where(IEnumerable<T>, Func<T, bool>)` instead of
`Queryable.Where(IQueryable<T>, Expression<>)` — the latter re-compiles the supplied
expression on every call when the underlying provider is the default LINQ-to-Objects
`EnumerableQuery<T>`.

### QueryEngine benchmarks (100-row tables) — post-Wave-14

| Method              | Mean        | Allocated    | vs Wave-12 baseline | vs original baseline |
|-------------------- |------------:|-------------:|--------------------:|---------------------:|
| ExecuteSimpleSelect |   815.0 us  |    207.3 KB  | unchanged           | 2.4x faster          |
| ExecuteJoinedSelect |     7.46 ms |   7113.9 KB  | **32x faster**, alloc up 2.7x | **48x faster** |

Source JSON: [`baseline-2026-05-10-wave14-QueryEngineBenchmarks.json`](baseline-2026-05-10-wave14-QueryEngineBenchmarks.json) and
[`baseline-2026-05-10-wave14-QueryEngineBenchmarks.md`](baseline-2026-05-10-wave14-QueryEngineBenchmarks.md).

### Parser benchmarks — post-Wave-14

| Method                              | Mean       | Allocated   |
|------------------------------------ |-----------:|------------:|
| ParseSimpleSelect_Ansi              |   4.01 us  |     ~7.6 KB |
| ParseSimpleSelect_MySql             |   4.48 us  |     ~7.6 KB |
| ParseComplexSelect_Ansi             |  57.26 us  |    ~65.1 KB |
| ParseComplexSelect_MySql            |  61.37 us  |    ~65.8 KB |
| ParseDeeplyNestedExpression_Ansi    |   3.66 us  |     ~8.5 KB |
| ParseCte_Ansi                       |  12.82 us  |    ~18.1 KB |
| ParseAndCreate_ComplexSelect_Ansi   |  98.71 us  |    ~82.6 KB |

Source JSON: [`baseline-2026-05-10-wave14-ParseBenchmarks.json`](baseline-2026-05-10-wave14-ParseBenchmarks.json) and
[`baseline-2026-05-10-wave14-ParseBenchmarks.md`](baseline-2026-05-10-wave14-ParseBenchmarks.md).

### Findings — post-Wave-14

1. **`ExecuteJoinedSelect` improved by ~32x (240 ms → 7.46 ms).** The fix exceeds the
   <100 ms target from issue #188's acceptance criteria by an order of magnitude. Two
   wins compound here: (a) the predicate compile happens exactly once per
   (shape, TDataRow, tableDataRow) tuple instead of once per FROM-row iteration
   (100x fewer `Lambda.Compile()` calls for the 100x100 cross product), and
   (b) the dispatch path now uses `Enumerable.Where(Func<T,bool>)` rather than
   `Queryable.Where(Expression<...>)` — the latter incurred an additional internal
   compile per call when the provider was the default `EnumerableQuery<T>`.
2. **`ExecuteSimpleSelect` is essentially unchanged (~810 us).** The simple SELECT
   has no JOIN cross product, so it only built the predicate ONCE in the Wave-12
   path; there was no per-row recompile to amortize away. Confirms the Wave-14 win
   is concentrated in the join hot path where it was profiled to live.
3. **Allocated bytes for `ExecuteJoinedSelect` rose from 2.6 MB to 7.1 MB (~2.7x).**
   The cached predicate captures the substitute-values dictionary in a per-call
   closure (the `row => predicate(row, substituteValues)` lambda inside
   `CompiledQueryDispatch.BuildApplyFilterCachedPredicateDelegate`) — that closure
   is allocated per FROM-row iteration. A future PR can hoist the closure to a
   single allocation per call (or use a struct-based capture) if allocation
   pressure becomes a concern. For now the 32x speed win dominates.
4. **Parse benchmarks are unchanged from Wave-12.** Issue #188 only touched the
   query-execution path; parse-time numbers should not move and they don't.

### Parser benchmarks — post-Wave-12

| Method                              | Mean       | Allocated  |
|------------------------------------ |-----------:|-----------:|
| ParseSimpleSelect_Ansi              |   4.29 us  |     ~7.6 KB |
| ParseSimpleSelect_MySql             |   4.36 us  |     ~7.6 KB |
| ParseComplexSelect_Ansi             |  57.39 us  |    ~65.1 KB |
| ParseComplexSelect_MySql            |  62.60 us  |    ~65.8 KB |
| ParseDeeplyNestedExpression_Ansi    |   3.72 us  |     ~8.5 KB |
| ParseCte_Ansi                       |  12.31 us  |          -  |
| ParseAndCreate_ComplexSelect_Ansi   |  98.67 us  |    ~82.6 KB |

Source JSON: [`baseline-2026-05-09-wave12-ParseBenchmarks.json`](baseline-2026-05-09-wave12-ParseBenchmarks.json) and
[`baseline-2026-05-09-wave12-ParseBenchmarks.md`](baseline-2026-05-09-wave12-ParseBenchmarks.md).

### Findings — post-Wave-12

1. **`ExecuteSimpleSelect` improved by ~2.4x (1.94 ms → 0.81 ms).** With one element
   type cached, the per-call cost of dispatch drops from MethodInfo.Invoke (~3-5 us
   plus boxing) to a direct delegate call (~2-3 ns). Allocated bytes are essentially
   unchanged (207.5 KB → 207.3 KB) — the dispatch was a CPU cost, not an allocation
   cost.
2. **`ExecuteJoinedSelect` improved by ~1.5x (360 ms → 240 ms).** The win is real
   but more modest than the speculative 5-10x target. The remaining cost lives
   inside `BuildExpression<TDataRow>` (which compiles a `Where` predicate
   expression-tree per call) and the per-row cross-product join enumeration. A
   future PR can amortize the predicate-compile via a second cache keyed on the
   `SqlBinaryExpression` shape — out of scope for #129.
3. **`ParseAndCreate_ComplexSelect_Ansi` apparent improvement (167 us → 99 us)
   is statistically inconclusive.** The Create() pipeline does not call
   `ReflectionHelper`, so it should not benefit from this refactor. The original
   number had a 18 us margin of error on a 167 us mean (11%) and the new number
   has 0.45 us margin on 99 us — the change is more likely run-to-run variance
   than signal. Worth re-measuring under a full job before attributing the
   improvement.

## CI workflow

GitHub Actions runs the benchmark suite on every PR that touches `src/Core/**`,
`src/Grammars/**`, `benchmarks/**`, or this folder via
[`.github/workflows/benchmarks.yml`](../../.github/workflows/benchmarks.yml). The
workflow:

1. Builds and runs the benchmark project in Release with `--job short --exporters json`
   so each run completes in roughly 3-5 minutes.
2. Uploads the full `BenchmarkDotNet.Artifacts/` tree as a workflow artifact.
3. Invokes [`.github/scripts/compare-benchmarks.ps1`](../../.github/scripts/compare-benchmarks.ps1)
   to diff each fresh `*-report-full.json` against the most recent committed baseline
   for the same benchmark class (alpha-sorted, last wins -- so adding
   `baseline-2026-06-01-...-ParseBenchmarks.json` automatically supersedes
   `baseline-2026-05-10-wave14-ParseBenchmarks.json`).
4. Posts the resulting markdown table as a sticky PR comment and as the workflow
   step summary, then exits non-zero (failing the check) when any benchmark
   exceeds the 10/20 threshold above.

### Runner choice

The workflow pins to `windows-latest`. The committed baselines were captured on a
Windows i7-1185G7 with .NET 10, and absolute BenchmarkDotNet numbers vary across
runner OS / CPU classes -- a Linux runner against a Windows baseline produces
spurious deltas in either direction. Switching to `ubuntu-latest` to align with
[`main.yml`](../../.github/workflows/main.yml) is reasonable but requires
re-recording every committed baseline on Linux as part of that switch.

GitHub-hosted runners are shared VMs and noisier than dedicated hardware. The
`--job short` config (3 warmups, 3 iterations) accepts wider confidence intervals
in exchange for keeping the workflow under 5 minutes; the 10/20 threshold is
conservative enough to ride out the noise. If false positives accumulate, the
workflow exposes `mean_threshold` and `alloc_threshold` inputs via
`workflow_dispatch` for tuning.

### Updating the baseline

When a PR intentionally changes hot-path numbers (a perf optimization lands or a
new feature legitimately adds cost), commit the new BenchmarkDotNet JSON output
alongside the code change:

1. Run the benchmark locally with the same `--job short` flag the workflow uses.
2. Copy the relevant `BenchmarkDotNet.Artifacts/results/*-report-full.json` files
   into `Docs/Benchmarks/` and rename them to follow the
   `baseline-YYYY-MM-DD-<tag>-<Class>.json` convention.
3. Add a new "## Post-Wave-N baseline" section to this README pointing at the
   new files and explaining what changed.
4. Include both the code change and the new baseline JSON in the same PR. The
   workflow's pairing rule will pick up the newer baseline on the next run.

Manual workflow runs are available via the **Actions** tab (`workflow_dispatch`)
when you need to re-run benchmarks against a branch without opening a PR.
