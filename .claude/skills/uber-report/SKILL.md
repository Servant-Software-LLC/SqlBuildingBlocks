---
name: uber-report
description: |
  Run the SqlBuildingBlocks five-agent assessment team against the current GitHub issue
  backlog and generate a unified status + readiness report with a lightweight local task
  index. Use at the start of a work session to get the current readiness picture, or after
  filing new issues to reassess sequencing and risks.
  The GitHub issues are the single source of truth for task descriptions and rationale.
  The local task index stores only execution metadata: filesExpected, dependsOn, status,
  wave tracking — for use by `/execute-tasks`.
---

# SqlBuildingBlocks Uber Report

Run the SqlBuildingBlocks agent team (Developer, QA, Architect, Devil's Advocate, Plan
Reviewer) against the current state of open GitHub issues, synthesize their findings into
a unified readiness report, and create a lightweight task index.

**Where things live:**
- Task descriptions, acceptance criteria, rationale → GitHub issues (source of truth)
- Execution metadata (filesExpected, dependsOn, wave tracking) → `.claude/tasks/*.tasks.json`
- Reports → `.claude/tasks/*.md`

**Repo coordinates:**
- Working tree: `C:/Dev/SqlBuildingBlocks`
- GitHub: `Servant-Software-LLC/SqlBuildingBlocks`

---

## Step 1: Gather Prior Context

1. Glob `.claude/tasks/*.tasks.json`, sort by path to find the latest (highest date, then
   highest version within that date).
2. If a prior tasks file exists, read it. Build two lists:
   - **Closed issues**: any task whose `ghIssue` number is now closed — confirm with
     `gh issue view <N> --json state --repo Servant-Software-LLC/SqlBuildingBlocks` and
     mark `status: "completed"` if `state` is `"CLOSED"`.
   - **Still-open tasks**: `pending`, `in_progress`, or `blocked` — carry forward as
     candidates for the new index.
3. If no prior tasks file exists, start fresh.

---

## Step 1.5: Work-in-Progress Survey

Before launching the assessment team, inventory **uncommitted and unmerged work**.

### 1.5.1 Run the survey

```bash
git -C C:/Dev/SqlBuildingBlocks status --short
git -C C:/Dev/SqlBuildingBlocks diff --stat
git -C C:/Dev/SqlBuildingBlocks diff --cached --stat
git -C C:/Dev/SqlBuildingBlocks stash list
git -C C:/Dev/SqlBuildingBlocks branch --no-merged main --format='%(refname:short) %(committerdate:relative) %(subject)'
git -C C:/Dev/SqlBuildingBlocks worktree list
git -C C:/Dev/SqlBuildingBlocks log @{upstream}..HEAD --oneline 2>/dev/null || echo "(no upstream tracking)"
```

For each non-empty result, capture filenames, branch names, stash IDs, commit subjects.
For unmerged branches and worktrees, also run
`git -C C:/Dev/SqlBuildingBlocks diff main...<branch> --stat`.

### 1.5.2 Build the WIP context block

```
## WIP Context (as of YYYY-MM-DD HH:MM)

### Uncommitted in main working tree
- `path/to/file` — modified (one-line summary)
- `path/to/new-file` — untracked (one-line summary)

### Stashes
- `stash@{0}` — "<message>" — touches: <files>

### Unmerged local branches
- `feat/foo` (N commits ahead; <date>) — touches: <files>

### Worktrees with changes
- `.claude/worktrees/agent-XYZ` — branch `worktree-agent-XYZ` — touches: <files>

### Unpushed commits on current branch
- `<hash>` — "<subject>"
```

If everything is clean: `## WIP Context: working tree clean, no stashes, no unmerged
branches, no worktrees with changes`.

---

## Step 2: Fetch Open GitHub Issues

```bash
gh issue list --repo Servant-Software-LLC/SqlBuildingBlocks --state open --json number,title,labels,body --limit 100
gh issue list --repo Servant-Software-LLC/SqlBuildingBlocks --state closed --json number,title,closedAt --limit 30
```

Group open issues into priority tiers based on labels, AGENTS.md review priorities, and
downstream-consumer impact:

- **P0 — Consumer blockers / parser-correctness incidents** (must land before anything else):
  - `#155` (transitive dependency lower bounds — **MockDB Docker build is blocked on this**)
  - Any issue tagged `top-5-priority` or matching the AGENTS.md P0 list (SQL injection
    vector in grammar, silent malformed-SQL accept, stack overflow on recursion,
    incorrect AST construction)
- **P1 — Library quality** (should land before next NuGet release):
  - Bug-labeled issues without a downstream consumer impact
  - Query engine gaps (#125, #127, #137) — affect runtime behavior for consumers
  - Missing test coverage (#128, #151)
  - CI / tooling drift (#130)
- **P2 — Library polish**:
  - Refactors and code-quality enhancements (#129, #132, #136)
  - Code scanning (#1)

Group classification is the orchestrator's first pass; the Architect / Devil's Advocate
agents are expected to challenge it.

---

## Step 3: Launch the Five-Agent Team

Read `AGENTS.md`, `README.md`, and the relevant `src/Core/` and `src/Grammars/` source
files before launching. Launch all five agents **in parallel** in a single message.
Each agent receives:
- The full list of open issues (number + title + body)
- The orchestrator's first-pass tier classification (above)
- A summary of issues closed since the prior report (if any)
- **The WIP Context block from Step 1.5** (verbatim)
- Their specific assessment lens
- The WIP-aware evaluation requirement below

### WIP-aware evaluation (applies to all agents)

For each open GitHub issue, every agent must classify it:

- **Not started** — no WIP overlap
- **Partially in flight** — WIP overlaps the issue's likely `filesExpected`; cite the file/branch/stash
- **Appears already done** — WIP looks like a complete (or near-complete) implementation

For any **proposed new issue** (would-be `<!-- NEW-ISSUE -->` block), the agent checks
whether the proposal's `filesExpected` overlaps any WIP. If yes, emit
`<!-- WIP-OVERLAP -->` instead of a new-issue block.

The `sqlbuildingblocks-developer` agent owns the deepest WIP-triage lens; any agent may
emit a `<!-- WIP-OVERLAP -->` block when their lens reveals one (e.g., QA spotting a
stashed test).

### Agent 1: SqlBuildingBlocks Developer (`sqlbuildingblocks-developer`)

Assess implementation readiness:
- Flag issues under-specified for safe implementation
- Identify the source files each issue would touch (for `filesExpected`) — be specific
  about NonTerminals, logical entities, query-engine paths
- Note code-state drift: areas where the code has changed since the issue was filed
  (e.g. #134 may already be resolved by the .NET 10 / global.json centralization work
  visible in `Directory.Build.props`)
- Surface implementation risks not captured in the issue body (Irony ambiguity, Create()
  switch updates, cross-cutting test impact)

### Agent 2: SqlBuildingBlocks QA (`sqlbuildingblocks-qa`)

Assess test coverage and quality readiness:
- Flag issues missing acceptance criteria or testability details
- Identify Core.Tests / dialect-specific tests / cross-cutting tests each PR should include
- Surface coverage gaps tied to AGENTS.md P0 priorities (silent malformed-SQL accept,
  stack overflow, incorrect AST round-trip)
- Recommend test additions that belong in each issue's scope
- Call out the PostgreSQL/SQL Server stub status as a structural quality risk where
  relevant

### Agent 3: SqlBuildingBlocks Architect (`sqlbuildingblocks-architecture`)

Validate dependency ordering and architectural soundness:
- Confirm the recommended sequencing is correct given current code state
- Flag circular or implicit dependencies
- Identify architectural concerns spanning multiple issues (e.g., #132 changing the
  `SqlExpression` shape would invalidate consumer-facing patterns assumed by other
  issues)
- Challenge any path that violates layer boundaries, the AnsiSQL-as-base inheritance
  model, the Create() naming dispatch, or the netstandard2.0 source TFM

### Agent 4: SqlBuildingBlocks Devil's Advocate (`sqlbuildingblocks-devils-advocate`)

Challenge priorities and surface hidden risks:
- Question whether the P0/P1 classification is correct
- Find assumptions baked into architectural decisions that may not hold
- Produce a risk register: top 5–10 risks with likelihood and impact ratings
- Identify work gaps that need a new GitHub issue before implementation begins
- Especially attack: any consumer-impact assumption (does FileBased.DataProviders /
  MockDB *actually* depend on the type / behavior we think they do?)

### Agent 5: SqlBuildingBlocks Plan Reviewer (`sqlbuildingblocks-plan-reviewer`)

Cross-check the synthesis itself:
- For each P0/P1 issue, does the proposed implementation plan address tests, error paths,
  and rollback?
- For #155 specifically: is the proposed minimum-version pinning compatible with what
  Irony 1.5.3 / HotChocolate need? Will it remove the MockDB workaround?
- Flag issues where the body is sufficient to implement vs needs the deep-researcher
  agent's pass first

**Important**: Each agent prompt must be self-contained — agents do not share
conversation history. Include AGENTS.md content and relevant source file contents in
each prompt.

### Structured new-issue output format

```
<!-- NEW-ISSUE
title: Short descriptive title (imperative, < 72 chars)
priority: P0|P1|P2|P3
effort: S|M|L
filesExpected:
  - src/path/to/File.cs
dependsOn:
  - 155
body:
  ## Context
  Why this work is needed and what gap it fills.

  ## Proposed Work
  What specifically needs to be done.

  ## Acceptance Criteria
  - [ ] First verifiable criterion
  - [ ] Second verifiable criterion
-->
```

- Use `filesExpected: []` and `dependsOn: []` if none apply.
- If two agents discover the same gap, each emits their block — orchestrator
  deduplicates by title similarity.
- Agents must NOT file issues themselves. The orchestrator files them after synthesis.

### Structured WIP-overlap output format

```
<!-- WIP-OVERLAP
ghIssue: 155                 # existing issue number, or "new" if it would have been new
wipItem: working-tree | stash@{N} | branch:<name> | worktree:<path>
overlapFiles:
  - Packages.props
overlapDescription: One-sentence summary of what the WIP appears to be doing
recommendedDisposition: land | fold | discard | park
rationale: Why
-->
```

Disposition vocabulary:
- **land** — commit on a branch, reference from the issue, mark partially-implemented
- **fold** — add the WIP's files to the relevant issue's `filesExpected`
- **discard** — document why; suggest reverting
- **park** — move to a named branch (`wip/<descriptor>`) with a tracking issue

---

## Step 4: Synthesize the Report

After all five agents return, produce one unified report:

```markdown
# SqlBuildingBlocks: Unified Status & Readiness Report

**Date**: [TODAY'S DATE]
**Analyzed by**: SqlBuildingBlocks agent team (Developer, QA, Architect, Devil's Advocate, Plan Reviewer)
**Prior report**: [path or "(none)"]
**Task index**: [path to .tasks.json]
**Changes since last report**: [issues opened or closed since last run]
**WIP at start**: [one-line summary]

---

## Part 0: Work In Progress

State of the working tree at the start of this report. Read this BEFORE the rest of the
report — these items shape what's already partway-done.

| WIP Item | Files | Overlaps | Disposition | Notes |
|----------|-------|----------|-------------|-------|

If working tree clean and no WIP: "Working tree clean at report time. No WIP
reconciliation needed."

---

## Executive Summary
[3–5 sentences: overall readiness, top blocker (issue #155 if still open), key risk,
recommended first action, material WIP outcomes]

## Part 1: Issue Status

### P0 — Consumer blockers / parser-correctness incidents
| Issue | Title | Status | Readiness | Notes |
|-------|-------|--------|-----------|-------|
| [#155](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/155) | Add minimum version constraints | ... | ... | MockDB Docker build is blocked on this |

### P1 — Library quality
| Issue | Title | Status | Readiness | Notes |

### P2 — Library polish
[Summary table]

## Part 2: Implementation Readiness (Developer Assessment)
[Per-issue notes: spec quality, key files, implementation concerns, code-state drift]

## Part 3: QA Coverage Gaps
[Per-issue test strategy: what tests are needed, which are missing]

## Part 4: Dependency Chain Health (Architect Assessment)
[Sequencing validation, architectural risks across issues]

## Part 5: Risk Register (Devil's Advocate)
[Top 5–10 risks: Risk | Likelihood | Impact | Mitigation]

## Part 6: Plan-Review Gating
[For each P0/P1 issue, plan reviewer's verdict: proceed / proceed with changes / rethink]

## Part 7: Newly Filed Issues
[Issues created by this run]

## Appendix A: Agent Agreement Matrix
[Where agents agreed vs. disagreed]

## Appendix B: Completed Issues (from prior reports)
[Issues closed since the prior report]
```

---

## Step 4.25: WIP Reconciliation

Before filing any new issues, reconcile WIP overlaps surfaced by the agents.

### 4.25.1 Collect WIP-overlap blocks
Scan all five agent outputs for `<!-- WIP-OVERLAP -->` blocks. Group by `wipItem`.

### 4.25.2 Surface to user
For each WIP item:
```
WIP item: <location> (<short description>)
  Overlaps: <issue list> (severity)
  Agent recommendations: <list>
  Choose: [L] land, [F] fold, [D] discard, [P] park
```
If no overlap blocks were emitted and the working tree was clean, skip to Step 4.5.

### 4.25.3 Apply dispositions
- **Land** — commit on `wip-land/<descriptor>`, post a comment on the overlapping issue,
  mark task `in_progress` with branch reference.
- **Fold** — add the WIP's files to the relevant task's `filesExpected`. Note in `notes`.
- **Discard** — record the decision in Part 0; user reverts manually.
- **Park** — move WIP to `wip/<descriptor>`, file tracking issue, record number in Part 0.

### 4.25.4 New-issue gate
After dispositions applied, re-evaluate every `<!-- NEW-ISSUE -->` block. Drop blocks
fully addressed by a WIP item now scheduled to land or fold.

### 4.25.5 Populate Part 0
Fill in Part 0 of the report with the WIP table.

---

## Step 4.5: File Newly Discovered Issues

### 4.5.1 Collect and deduplicate
Scan agent outputs for `<!-- NEW-ISSUE -->` blocks. Merge if title similarity > ~80%; use
the more detailed body, combine `filesExpected`, note both agents in "Discovered by".

### 4.5.2 Confirm with user (P0 blocks)
For `priority: P0`, print the proposed title and one-line summary, ask user to confirm.

### 4.5.3 File each issue
```bash
gh issue create \
  --repo Servant-Software-LLC/SqlBuildingBlocks \
  --title "<title from block>" \
  --body "<body, with 'Discovered by: /uber-report YYYY-MM-DD (SqlBuildingBlocks <Agent>)' prepended>"
```

If `gh issue create` fails, log and continue.

### 4.5.4 Record newly filed issues
Capture `{ ghIssue, title, priority, effort, filesExpected, dependsOn }` for each. These
go in the task index in Step 5 and Part 7 of the report.

---

## Step 5: Generate the Task Index

Create a `.tasks.json` alongside the report. Index covers P0 and P1 issues only.

**Naming**: `YYYY-MM-DD-uber-report-vN.tasks.json`

### Schema

```json
{
  "indexDate": "YYYY-MM-DD",
  "indexVersion": 1,
  "milestone": "library-quality",
  "reportFile": "YYYY-MM-DD-uber-report-vN.md",
  "tasksFile": "YYYY-MM-DD-uber-report-vN.tasks.json",
  "priorTasksFile": "path/to/prior.tasks.json or null",
  "tasks": [
    {
      "ghIssue": 155,
      "priority": "P0",
      "effort": "S",
      "status": "pending",
      "filesExpected": [
        "Packages.props",
        "Directory.Build.props",
        "src/Core/SqlBuildingBlocks.Core.csproj",
        "src/Grammars/MySQL/SqlBuildingBlocks.Grammars.MySQL.csproj"
      ],
      "dependsOn": [],
      "completedDate": null,
      "completedCommit": null,
      "completedWave": null,
      "notes": "MockDB Docker build is blocked on this — see MockDB Directory.Build.props NoWarn workaround"
    }
  ],
  "completed": []
}
```

### Field definitions

| Field | Type | Description |
|-------|------|-------------|
| `ghIssue` | number | GitHub issue number — primary identifier |
| `priority` | string | P0, P1, P2, P3 |
| `effort` | string | S (< 1 day), M (1–3 days), L (1–2 weeks) |
| `status` | string | `pending`, `in_progress`, `completed`, `skipped`, `blocked`, `manual_required` |
| `filesExpected` | array | Files the implementation is expected to touch |
| `dependsOn` | array | GH issue numbers that must be completed first |
| `completedDate` | string/null | ISO date when completed |
| `completedCommit` | string/null | Git commit hash |
| `completedWave` | number/null | Wave number that completed this |
| `notes` | string | Implementation observations, blockers, partial-work details |

### Populating `filesExpected`

Use the Developer agent's assessment. Be specific — list exact file paths.

### Populating `dependsOn`

Use the Architect-approved sequencing. Issue #155 has no dependencies (centralized
package management change). Most query-engine issues depend only on themselves; only
issues that touch shared NonTerminals or `SqlExpression` shape have cross-issue
dependencies.

---

## Step 6: Save Everything

1. Get today's date: `YYYY-MM-DD`
2. Determine version `N`:
   - Glob `.claude/tasks/YYYY-MM-DD-uber-report-v*.md`
   - First report of the day: `N=1`. Subsequent: next available.
3. Write report: `.claude/tasks/YYYY-MM-DD-uber-report-vN.md`
4. Write index: `.claude/tasks/YYYY-MM-DD-uber-report-vN.tasks.json`
5. Report both file paths to the user.
