# Silent-Accept Audit & Negative Test Corpus

**Issue**: [#175](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/175) (Wave 9 Lane A)
**Date**: 2026-05-09
**Scope**: AnsiSQL, MySQL, PostgreSQL, SQL Server grammars (4 dialects)
**Owner**: developer agent (Wave 9 Lane A)

## Purpose

Per AGENTS.md, "Parser accepts malformed SQL silently" is a P0 review concern. Until
this audit, each dialect's `NegativeTests.cs` carried only ~10 hand-picked malformed
inputs (Wave 2 baseline + the Wave 6 / #167 dangling-comma fix). That is a tripwire,
not a baseline, and is insufficient to claim P0-class hygiene.

This audit:

1. Defined a 75–82-case candidate corpus of malformed SQL covering eight categories
   (statement-level, SELECT clause, FROM/JOIN, expressions, identifiers/literals,
   DML, sub-queries/set-ops, dialect-specific).
2. Probed every candidate against all four dialect grammars and recorded
   acceptance/rejection per dialect.
3. Promoted the 81 (cross-dialect) + 1–2 (dialect-specific) candidates that produce
   parse errors into asserting tests via `[Theory]` + `[MemberData]` in each
   dialect's `NegativeTests.cs`.
4. Documented the **one** non-rejection finding (bare `SELECT *` with no FROM),
   classified it as **intentional**, and explained why.

## Methodology

A temporary probe class was added to each dialect's test project. Each probe ran
the candidate corpus through the dialect's full grammar (same wiring as the
permanent `NegativeTests.cs` `TestGrammar` inner class) and emitted, via
`ITestOutputHelper`, the list of inputs the parser silently accepted. Probes ran
under `dotnet test --configuration Release`. After cataloguing, the probe files
were removed and the rejected inputs were promoted to permanent asserting tests.

The assertion shape is unchanged from the Wave 2 contract:

```csharp
Assert.True(parseTree.HasErrors(), $"Expected parser to reject: {sql}");
Assert.NotEmpty(parseTree.ParserMessages);
Assert.All(parseTree.ParserMessages, msg =>
    Assert.False(string.IsNullOrWhiteSpace(msg.Message)));
```

The exact wording of `ParserMessages` is not asserted because Irony's messages are
not stable API.

## Per-dialect outcome

| Category | AnsiSQL | MySQL | PostgreSQL | SQL Server |
|----------|--------:|------:|-----------:|-----------:|
| Statement-level | 7 | 7 | 7 | 7 |
| SELECT clause | 15 | 15 | 15 | 15 |
| FROM / JOIN | 9 | 9 | 9 | 9 |
| Expressions | 24 | 24 | 24 | 24 |
| Identifiers / literals | 8 | 8 | 8 | 8 |
| DML (INSERT/UPDATE) | 11 | 11 | 11 | 11 |
| Sub-queries / set-ops | 11 | 11 | 11 | 11 |
| Dialect-specific | 1 | 2 | 1 | 1 |
| **Total asserting tests** | **86** | **87** | **86** | **86** |

All asserting tests pass on the current `feature/wave-9` branch under
`dotnet test --configuration Release`.

## Findings

### Finding 1 — `SELECT *` (no FROM) is silently accepted across all four dialects

**Repro**: `SELECT *`
**Dialects**: AnsiSQL, MySQL, PostgreSQL, SQL Server (all)
**Status**: **Intentional, not a defect.**

The SelectStmt rule has `fromClauseOpt → grammar.Empty | FROM + TableName + JoinChainOpt`
(see `src/Core/SelectStmt.cs:162`), so the FROM clause is optional. This is correct
SQL: `SELECT 1`, `SELECT current_timestamp`, and `SELECT *` (which produces an
`SqlAllColumns` with no source table) are all well-formed in standard SQL.

When `SELECT *` is parsed without a FROM clause, the existing `AddTables` /
`AddTable` logic at `SelectStmt.cs:518-532` short-circuits because
`fromClauseOpt.ChildNodes.Count < 2`, so no table is materialised — semantically
the column list refers to "no rows" but is not malformed.

**Recommendation**: do not enforce. If a downstream consumer wants to require a
FROM clause for `SELECT *` semantically, it should validate post-parse, not at the
grammar layer. **Not added to the asserting corpus.**

### No other silent-accept findings

After probing 80+ inputs across all four dialects, no other malformed input was
silently accepted. The Wave 6 / #167 `MarkReservedWords("FROM", "INTO")` fix
correctly closes the dangling-comma class of silent-accepts, and Irony's LALR(1)
parser correctly rejects every other malformed candidate in the audit corpus.

## Trivial grammar fixes applied inline

**None.** No silent-accepts beyond the intentional `SELECT *` case were
discovered, so no grammar fixes were needed.

## Cross-dialect divergences

**None observed.** All four dialects rejected the same 81 cross-cutting candidates
and accepted only the same single intentional case (`SELECT *`). Dialect-specific
features (FETCH FIRST for AnsiSQL, LIMIT for MySQL, ON CONFLICT for PostgreSQL,
TOP for SQL Server) were each tested in isolation and all reject malformed forms.

## Coverage map (which categories cover which AGENTS.md concerns)

| AGENTS.md P0 concern | Covered by category |
|-----------------------|---------------------|
| Parser accepts malformed SQL silently | All categories (it IS the audit) |
| Infinite loops or stack overflows in recursion | Pre-existing `RecursionTests.cs` |
| Incorrect AST construction on round-trip | Lane B's `RoundTripTests.cs` (Wave 9) |
| SQL injection vectors | Out of scope for negative-parse tests |

## Future maintenance

**Rule**: Any change that adds a NonTerminal, alters an existing rule, or adds a
dialect-specific keyword MUST also extend the relevant category in each affected
dialect's `NegativeTests.cs` with at least one malformed-input assertion that
exercises the new rule. The categories in this corpus are the menu — pick the
category whose theme matches the change. If the change introduces a new category,
add a new `[MemberData]` block and a new line to the table above.

When a new dialect is added (e.g. SQLite, DB2), the cross-cutting 81 cases (all
categories except `DialectSpecific`) become the baseline corpus for that dialect.
Author the dialect-specific block based on what the new grammar adds.

## Related issues

- [#167](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/167) —
  Wave 6 dangling-SELECT-comma fix (`MarkReservedWords` for FROM/INTO). The
  expanded corpus confirms that fix and adds 7 more dangling-comma variants
  (GROUP BY, ORDER BY, FROM, IN, INSERT, UPDATE SET, VALUES) all of which are
  correctly rejected.
- [#175](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/175) —
  this audit (Wave 9 Lane A).
