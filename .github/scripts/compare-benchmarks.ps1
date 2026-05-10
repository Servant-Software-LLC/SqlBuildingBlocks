<#
.SYNOPSIS
    Compares fresh BenchmarkDotNet JSON output against committed baseline JSON files.

.DESCRIPTION
    Reads a directory of new BenchmarkDotNet `*-report-full.json` files (the kind
    `--exporters json` produces in `BenchmarkDotNet.Artifacts/results/`), pairs each
    one with a committed baseline JSON in `Docs/Benchmarks/`, and emits a markdown
    delta table to stdout. Exits with a non-zero status when any benchmark exceeds
    the regression threshold.

    Pairing rule: a new file `*-<Class>-report-full.json` is matched to the
    most-recently-named baseline whose filename ends with `-<Class>.json` (alpha-sorted,
    last wins -- so `baseline-2026-05-10-wave14-ParseBenchmarks.json` beats the older
    Wave-9 file).

    Regression rule (matches `Docs/Benchmarks/README.md`):
      - Mean is more than 10 percent slower than the baseline, OR
      - Allocated bytes are more than 20 percent higher than the baseline.

    Allocation deltas are skipped when the baseline reports 0 bytes (BenchmarkDotNet
    rounds sub-microsecond methods to 0; not signal we can divide by).

.PARAMETER ResultsDir
    Path to the directory holding fresh BenchmarkDotNet JSON output. Default:
    `benchmarks/SqlBuildingBlocks.Benchmarks/BenchmarkDotNet.Artifacts/results`.

.PARAMETER BaselineDir
    Path to the directory holding committed baseline JSON. Default: `Docs/Benchmarks`.

.PARAMETER MeanThreshold
    Fractional regression threshold for mean. Default 0.10 (10 percent).

.PARAMETER AllocThreshold
    Fractional regression threshold for allocated bytes. Default 0.20 (20 percent).

.PARAMETER SummaryPath
    Optional file to write the markdown summary to (in addition to stdout). Used by
    the GitHub Actions workflow to populate `$GITHUB_STEP_SUMMARY` and a PR comment.

.EXAMPLE
    pwsh .github/scripts/compare-benchmarks.ps1
#>
[CmdletBinding()]
param(
    [string]$ResultsDir = "benchmarks/SqlBuildingBlocks.Benchmarks/BenchmarkDotNet.Artifacts/results",
    [string]$BaselineDir = "Docs/Benchmarks",
    [double]$MeanThreshold = 0.10,
    [double]$AllocThreshold = 0.20,
    [string]$SummaryPath = ""
)

$ErrorActionPreference = "Stop"

function Get-BenchmarkClassFromFile {
    param([string]$FileName)
    # Matches both new files (`Foo.Bar.ParseBenchmarks-report-full.json`) and baseline
    # files (`baseline-2026-05-10-wave14-ParseBenchmarks.json`). The class name is the
    # last hyphenated segment before `.json` (or `-report-full.json`).
    $bare = $FileName -replace '-report-full\.json$', '.json'
    $bare = $bare -replace '\.json$', ''
    # Strip leading namespace if present (everything before the final `.`)
    if ($bare.Contains('.')) {
        $bare = $bare.Substring($bare.LastIndexOf('.') + 1)
    }
    # Strip leading baseline-* prefix if present (everything up to the last `-` segment).
    # We want the trailing class token (e.g. ParseBenchmarks) regardless of date/wave tags.
    if ($bare -match '-([A-Za-z][A-Za-z0-9]*Benchmarks)$') {
        return $Matches[1]
    }
    return $bare
}

function Load-Benchmarks {
    param([string]$Path)
    $json = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    $entries = @{}
    foreach ($b in $json.Benchmarks) {
        $entries[$b.Method] = [pscustomobject]@{
            Method    = $b.Method
            Mean      = [double]$b.Statistics.Mean
            Allocated = [int64]$b.Memory.BytesAllocatedPerOperation
        }
    }
    return $entries
}

function Get-LatestBaselineFor {
    param(
        [string]$BaselineDir,
        [string]$ClassName
    )
    $candidates = Get-ChildItem -LiteralPath $BaselineDir -Filter "*-$ClassName.json" |
        Sort-Object Name
    if ($candidates.Count -eq 0) { return $null }
    return $candidates[-1].FullName
}

if (-not (Test-Path -LiteralPath $ResultsDir)) {
    Write-Error "Results directory not found: $ResultsDir"
    exit 2
}
if (-not (Test-Path -LiteralPath $BaselineDir)) {
    Write-Error "Baseline directory not found: $BaselineDir"
    exit 2
}

$resultFiles = Get-ChildItem -LiteralPath $ResultsDir -Filter '*-report-full.json' -ErrorAction SilentlyContinue
if (-not $resultFiles) {
    Write-Error "No '*-report-full.json' files found in $ResultsDir. Did the benchmark run with '--exporters json'?"
    exit 2
}

$lines = @()
$lines += "# Benchmark regression report"
$lines += ""
$lines += "Threshold: mean +$([math]::Round($MeanThreshold * 100))% or allocated +$([math]::Round($AllocThreshold * 100))%."
$lines += ""

$anyRegression = $false
$rows = @()

foreach ($file in $resultFiles) {
    $className = Get-BenchmarkClassFromFile -FileName $file.Name
    $baselinePath = Get-LatestBaselineFor -BaselineDir $BaselineDir -ClassName $className
    if (-not $baselinePath) {
        $lines += "## $className"
        $lines += ""
        $lines += "_No baseline found for $className -- skipping comparison. Commit a baseline JSON named ``*-$className.json`` under ``$BaselineDir`` to enable regression detection._"
        $lines += ""
        continue
    }

    $newEntries = Load-Benchmarks -Path $file.FullName
    $baseEntries = Load-Benchmarks -Path $baselinePath

    $lines += "## $className"
    $lines += ""
    $lines += "Baseline: ``$([System.IO.Path]::GetFileName($baselinePath))``"
    $lines += ""
    $lines += "| Method | Baseline mean (ns) | New mean (ns) | Mean delta | Baseline alloc (B) | New alloc (B) | Alloc delta | Verdict |"
    $lines += "|--------|-------------------:|--------------:|-----------:|-------------------:|--------------:|------------:|---------|"

    foreach ($method in ($newEntries.Keys | Sort-Object)) {
        $new = $newEntries[$method]
        $base = $baseEntries[$method]
        if (-not $base) {
            $lines += "| $method | _missing_ | $([math]::Round($new.Mean, 0)) | n/a | _missing_ | $($new.Allocated) | n/a | _new method, no baseline_ |"
            continue
        }

        $meanDelta = if ($base.Mean -gt 0) { ($new.Mean - $base.Mean) / $base.Mean } else { 0 }
        $allocDelta = if ($base.Allocated -gt 0) { [double]($new.Allocated - $base.Allocated) / [double]$base.Allocated } else { $null }

        $verdict = "ok"
        if ($meanDelta -gt $MeanThreshold) {
            $verdict = "**REGRESSION (mean +$([math]::Round($meanDelta * 100, 1))%)**"
            $anyRegression = $true
        }
        elseif ($null -ne $allocDelta -and $allocDelta -gt $AllocThreshold) {
            $verdict = "**REGRESSION (alloc +$([math]::Round($allocDelta * 100, 1))%)**"
            $anyRegression = $true
        }

        $meanDeltaStr = "{0:+0.0;-0.0;0.0}%" -f ($meanDelta * 100)
        $allocDeltaStr = if ($null -eq $allocDelta) { "n/a" } else { "{0:+0.0;-0.0;0.0}%" -f ($allocDelta * 100) }

        $lines += "| $method | $([math]::Round($base.Mean, 0)) | $([math]::Round($new.Mean, 0)) | $meanDeltaStr | $($base.Allocated) | $($new.Allocated) | $allocDeltaStr | $verdict |"
    }

    $lines += ""
}

$lines += ""
if ($anyRegression) {
    $lines += "**Result: regressions detected.** See rows marked REGRESSION above."
} else {
    $lines += "**Result: no regressions detected.**"
}

$summary = $lines -join [Environment]::NewLine
Write-Output $summary

if ($SummaryPath) {
    $summary | Out-File -LiteralPath $SummaryPath -Encoding utf8
}

if ($anyRegression) { exit 1 } else { exit 0 }
