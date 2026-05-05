---
name: sqlbuildingblocks-architecture-knowledge
description: |
  Architect's lens on SqlBuildingBlocks. Use when reasoning about system shape, layer
  boundaries, NonTerminal seams, dialect inheritance, query-engine layering, or the
  evolution of the architecture itself (not day-to-day coding).

  Load this when:
  - Proposing a new grammar dialect, NonTerminal seam, or query-engine extension
  - Evaluating whether a change respects layer boundaries (Core vs Grammars), Create() dispatch, or consumer NuGet contracts
  - Reviewing designs for grammar ambiguity, dialect parity, or downstream-consumer impact
  - Planning the PostgreSQL or SQL Server grammar build-out

  Do NOT load this skill for "where is file X / how do I add a NonTerminal" -- that is
  `sqlbuildingblocks-dev-knowledge`. Do NOT load this for product/consumer/maturity
  questions -- that is `sqlbuildingblocks-domain-knowledge`.

  SELF-UPDATING: When your work changes, advances, or extends the architecture (new
  layers, new grammar dialects, query-engine layer changes, resolved architectural gaps),
  update this skill in the same deliverable. Update only the affected section(s).
---

# SqlBuildingBlocks Architecture Knowledge

## Architectural Style and Principles

SqlBuildingBlocks is a **layered, dialect-pluggable SQL parser library** with a separate in-memory query engine. It is consumed as a set of NuGet packages by other Servant Software products (FileBased.DataProviders, MockDB).

Guiding principles:
1. **Grammar is data; logical entities are semantics.** Irony NonTerminals describe the BNF; logical entity classes (`SqlSelectDefinition`, `SqlBinaryExpression`, etc.) describe meaning. The boundary between them is the `NonTerminal.Create(ParseTreeNode)` factory.
2. **AnsiSQL is the base; dialects extend, never replace.** MySQL, PostgreSQL, and SQL Server grammars subclass `SqlGrammar` and override only what differs. Duplicating an AnsiSQL production inside a dialect is a smell.
3. **One core, many dialects, many consumers.** The Core package is shared across every dialect; logical entities and the query engine live in Core. Dialect packages add only the productions and terminal factories they need.
4. **Parse, then resolve.** Parsing produces a logical entity graph with unresolved references; `ResolveReferences()` is a separate pass driven by external schema/function providers. Mixing the two layers couples the parser to runtime data.
5. **Layer down, never sideways.** AnsiSQL Grammar -> Core (NonTerminals + entities) -> Irony. Dialects -> their own NonTerminal overrides + Core. QueryEngine -> Core entities + ITableDataProvider. Peer layers do not reach into each other.

## Layer Boundaries

Top-down. Each layer depends only downward.

```
+------------------------------------------------------------+
| Consumers (FileBased.DataProviders, MockDB) -- via NuGet   |
+------------------------------------------------------------+
| Grammars: AnsiSQL | MySQL | PostgreSQL (stub) | SQLServer (stub) |
|   SqlGrammar subclass + dialect-specific NonTerminals      |
+------------------------------------------------------------+
| Core                                                       |
|   NonTerminals (Stmt, SelectStmt, Expr, Id, ...)           |
|   Logical entities (SqlSelectDefinition, SqlExpression, ...) |
|   QueryEngine + ITableDataProvider, ITableSchemaProvider   |
|   Visitors (ISqlExpressionVisitor, ISqlValueVisitor)       |
+------------------------------------------------------------+
| Irony 1.5.3 (LR parser, ParseTree, NonTerminal base type)  |
+------------------------------------------------------------+
```

## Load-Bearing Invariants

Violating any of these is an architectural incident:

1. **NonTerminal `Create()` dispatch matches on `expression.Term.Name` strings.** Renaming a NonTerminal without updating every Create() switch case is a silent AST construction failure. Any new NonTerminal that's referenced by name from a parent's Create() must have its name pinned in code (not derived from the class name).
2. **AnsiSQL is the base grammar.** Dialect grammars extend by overriding NonTerminals, not by reimplementing AnsiSQL productions. A change to AnsiSQL's `Expr.Rule` cascades to MySQL/PostgreSQL/SQL Server -- this is intentional and required for cross-dialect consistency.
3. **Source TFM is netstandard2.0.** Source projects target netstandard2.0 so consumers on .NET Framework, .NET Standard, and .NET 6+ can all reference the NuGet packages. Test projects target net10.0. Adding a netstandard2.1+ API to a source project breaks the consumer surface.
4. **The NuGet package surface is a public contract.** Logical entity types (`SqlSelectDefinition`, `SqlBinaryExpression`, etc.), NonTerminal types, and visitor interfaces are consumed by FileBased.DataProviders and MockDB. Renaming or restructuring them without coordination is an on-the-wire breaking change.
5. **Grammar package boundaries match NuGet package boundaries.** `src/Grammars/MySQL/` ships as `SqlBuildingBlocks.Grammars.MySQL`. Cross-dialect code goes in Core, never in a sibling Grammar.
6. **Parse vs resolve is a strict order.** A NonTerminal's `Create` method must not require `ITableSchemaProvider` or `IFunctionProvider` -- those are consulted later by `ResolveReferences()`. Coupling them at parse time would make the parser unusable without a full schema.
7. **Query engine consumes logical entities, not ParseTreeNodes.** The execution path is `SqlSelectDefinition -> QueryEngine -> VirtualDataTable`. Threading Irony types into the engine would couple execution to the parser implementation.

## Grammar Plug-in Pattern

Every dialect implements the same shape:

**Seams in Core:**
- `NonTerminal` subclasses with virtual `Rule` (BNF expression) and `Create(ParseTreeNode)` factory
- `TerminalFactory` (Irony) -- abstract source of identifiers, literals, keywords
- `SqlGrammar` -- Irony grammar root that wires the NonTerminals together
- Logical entity types -- the output of `Create()`

**Per-dialect overrides:**
- Subclass `SqlGrammar` (e.g. `MySqlGrammar`)
- Override `TerminalFactory` for identifier-quoting style (backticks for MySQL, double quotes for ANSI, brackets for SQL Server)
- Override specific NonTerminals where the dialect differs (e.g. MySQL's `Expr` adds `INTERVAL`)
- Add dialect-specific productions (LIMIT/OFFSET for MySQL, FETCH FIRST for ANSI, TOP for SQL Server)

**Why this shape:** a new dialect never modifies Core. It subclasses, overrides, and contributes its delta. This is what makes the PostgreSQL and SQL Server build-outs feasible without forking AnsiSQL.

**Asymmetry today:** AnsiSQL and MySQL are production-quality. PostgreSQL and SQL Server are stubs (see domain-knowledge maturity table). Architecturally they fit the same mold; they just haven't been filled in.

## Query Engine Architecture

`QueryEngine` (`src/Core/QueryProcessing/QueryEngine.cs`) executes parsed `SqlSelectDefinition` against `ITableDataProvider`-supplied rows. The architectural shape:

```
SqlSelectDefinition
    |
    v
QueryEngine.Execute()
    |-- Validate features (throw NotImplementedException on unsupported constructs)
    |-- Resolve CTEs (recursive walk through WITH bindings)
    |-- Determine output columns
    |-- Pull rows via ITableDataProvider / IAllTableDataProvider (for JOINs)
    |-- Build LINQ Expression<Func<T, bool>> from WHERE/HAVING SqlExpressions
    |-- Apply: filter, GROUP BY, aggregates, window functions, ORDER BY, LIMIT/OFFSET
    |
    v
VirtualDataTable (streaming results)
```

**Load-bearing engine constraints:**
- The engine compiles `SqlExpression` trees to LINQ `Expression<Func<T, bool>>`. Every new `SqlExpression` arm requires a matching translator entry, or execution throws.
- Unsupported features throw `NotImplementedException`. Consumers depend on this signal — don't replace it with silent fallback.
- The engine is in-memory only. Adding pushdown to a backing store is a different abstraction (a new `IQueryProvider`), not an extension of `QueryEngine`.

## Consumer Surface (NuGet Contract)

Five packages ship to NuGet.org:
- `SqlBuildingBlocks.Core`
- `SqlBuildingBlocks.Grammars.AnsiSQL`
- `SqlBuildingBlocks.Grammars.MySQL`
- `SqlBuildingBlocks.Grammars.PostgreSQL`
- `SqlBuildingBlocks.Grammars.SQLServer`

**Consumers** (verify before changing public types):
- `FileBased.DataProviders` (`C:/Dev/FileBased.DataProviders`) -- ADO.NET providers for JSON/XML/CSV that parse SQL via this library.
- `MockDB` (`C:/Dev/MockDB`) -- protocol endpoints (MySQL/PostgreSQL/SQL Server wire) that parse incoming SQL via this library.

**Consumer-impacting changes** (require ADR-level coordination):
- Renaming or restructuring logical entity types (`SqlSelectDefinition`, `SqlBinaryExpression`, etc.).
- Changing the type returned by a public NonTerminal's `Create()`.
- Bumping the major version of a transitive dependency (e.g., Irony, Newtonsoft.Json) — see issue #155 for the current MockDB-blocking gap.
- Changing the source TFM away from netstandard2.0.
- Splitting or merging Grammar packages.

## Cross-Cutting Concerns

| Concern | Owner | Notes |
|---|---|---|
| **Build / SDK pinning** | `global.json`, `Directory.Build.props` | .NET 10 SDK with latestMinor rollForward. Source TFM = netstandard2.0; test TFM = net10.0. Centralizing SDK version is a recently-resolved gap (Directory.Build.props NetTFM property). |
| **Package versions** | `Packages.props` | Centralized PackageReference Update entries for Irony, xUnit, Moq, BenchmarkDotNet. Adding a transitive lower-bound for `Newtonsoft.Json` is the open work for #155. |
| **TreatWarningsAsErrors** | `Directory.Build.props` | Enabled. Any new NU1602/NU1701/NU1903 warning fails the build. This is what surfaced #155. |
| **CI** | `.github/workflows/` | ubuntu-latest, .NET 10. Issue #130 tracks deprecated GitHub Actions versions. |
| **Coverage** | Codecov | PR coverage comments. Baseline lives in Codecov, not in-repo. |
| **AGENTS.md review priorities** | repo root | P0: SQL injection vectors in grammar, silent malformed-SQL accept, infinite-loop / stack-overflow recursion, incorrect AST construction. These are architectural quality gates, not day-to-day code review items. |

## Evolutionary Seams (What Is Expected to Change)

These are designed extension points:

1. **PostgreSQL grammar build-out.** Stub today. Adding RETURNING, array types, JSON operators, distinct PostgreSQL data types. Consumes the same plug-in pattern as MySQL.
2. **SQL Server grammar build-out.** Stub today. Adding TOP, OUTPUT, MERGE, table hints, square-bracket identifier quoting.
3. **Query engine feature coverage.** Window functions execution (#137), CTE execution (#125), error handling (#127), generic-method-dispatch performance (#129) are all open. The engine layer accepts new arms in `SqlExpression` and new visitors without architectural change.
4. **`SqlExpression` discriminated union enforcement.** Issue #132 -- today multiple properties can be set simultaneously, which is a latent invariant violation. Tightening this is a public-surface change.
5. **Integration tests for consumer scenarios.** Issue #151 -- a new test project exercising real FileBased.DataProviders and MockDB usage patterns would catch consumer breakage before NuGet release.
6. **Grammar / SQL compatibility matrix as published documentation.** Today only in `sqlbuildingblocks-qa-knowledge`. Promoting it to user-facing docs is a doc-writer task.

## Load-Bearing Decisions (Do NOT Change Silently)

These ripple across the system. Any proposal to change one requires an ADR-level conversation:

1. **Irony as the parser engine.** Switching to ANTLR or hand-rolled parsing would rewrite every NonTerminal. Don't.
2. **netstandard2.0 source TFM.** Bumping this drops .NET Framework consumers. Requires explicit communication with FileBased.DataProviders and MockDB.
3. **MIT license + open-source on GitHub.** Consumer integrations and external contributors depend on this.
4. **AnsiSQL as the base dialect.** Reorganizing so MySQL or PostgreSQL becomes the base would invert the inheritance and break dialect isolation.
5. **NuGet package decomposition (Core + 4 dialect packages).** Merging into a single package would force consumers to take all dialects; splitting further would force consumers into multi-package upgrades.
6. **`Create()`-as-factory pattern.** The string-name dispatch is fragile but pervasive. Replacing it (e.g., with attributes or registration) is a cross-cutting refactor, not an opportunistic change.

## When to Update Architecture-Level Docs

Update this skill when:
- A new layer, package, or grammar dialect appears.
- A load-bearing invariant changes (e.g., source TFM bump).
- A cross-cutting concern moves (e.g., centralizing package management for transitive lower bounds — issue #155).
- The query engine gains a new architectural seam (e.g., a translation registry instead of a hard-coded switch).

Do **not** update for: a new NonTerminal added to an existing dialect, a query-engine bug fix, a new logical entity field, or refactors that don't change layer shape.

## Pointers

- Developer cookbook (file paths, "how do I add X"): `sqlbuildingblocks-dev-knowledge`
- Product/consumer/maturity: `sqlbuildingblocks-domain-knowledge`
- Test strategy and coverage: `sqlbuildingblocks-qa-knowledge`
- Open architectural items: GitHub issues #155 (consumer-blocking), #132 (discriminated union), #151 (integration tests), #137/#125 (query engine gaps) on Servant-Software-LLC/SqlBuildingBlocks
