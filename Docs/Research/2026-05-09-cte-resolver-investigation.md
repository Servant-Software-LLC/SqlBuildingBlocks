# CTE Resolver Investigation — #174 (Parsed non-recursive CTE end-to-end execution)

Repo: `C:/Dev/SqlBuildingBlocks` @ `main` (HEAD `978a628`)
Date: 2026-05-09
Author: deep-researcher agent
Related issues: #174 (this), #168 (recursive CTE execution; depends on this)

## 1. Executive summary

The Plan Reviewer's hypothesis is **confirmed by the code, with a refinement**. The root cause is structural, not a missed assignment: `SelectReferenceResolver` only knows about tables visible through `tablesInSelect` (FROM/JOIN) and `outerTablesInScope` (correlated outer queries). CTEs declared via `SqlSelectDefinition.Ctes` are a third category of named, table-shaped scope that the resolver never sees. The hand-built tests pass because they bypass resolution and pre-set `SqlColumn.TableRef`; the parsed path runs through `SelectStmt.Create(node, db, db)` which calls `ResolveReferences()`, which then fails the column lookup because the CTE name `AllOrders` is neither in FROM/JOIN of the main SELECT (it is the FROM table, but its schema cannot be obtained from `ITableSchemaProvider`) nor in any outer scope.

**Recommendation: Option A (hybrid leaning A)** — pre-resolve each CTE's `SelectDefinition`, then surface the CTE as a CTE-aware `SqlTable` in `outerTablesInScope` for the main query. The existing `SqlDerivedTable` machinery is the precedent: it already carries a `SelectDefinition`, is recognised by `SelectDefinitionColumns.GetColumns`, and is skipped by `ResolveTablesDatabase`. A CTE is "a named, top-level derived table." Reusing this seam is small (single-digit method count) and naturally supports nested-CTE-references-prior-CTE.

**Scope: S/M (small-medium).** Changes confined to `SelectReferenceResolver` (CTE pre-pass) and `SqlSelectDefinition.ResolveReferences` (orchestration). New `SqlCteTable : SqlTable` (or reuse `SqlDerivedTable` with the CTE name as alias) is the only new type. No public-API breaking changes for downstream consumers.

**Risk: low for #174, moderate for #168.** Recursive CTEs need the resolver to bind the recursive term's columns to the CTE itself before the body is fully resolved (a forward-reference) — Option A covers this if we surface the CTE name with its anchor's projection schema before resolving the recursive term. See §7.

## 2. Root cause — evidence

### 2.1 Resolver has no CTE awareness

`src/Core/Utils/SelectReferenceResolver.cs:1-473` contains zero references to `Ctes`, `WithClause`, `SqlCteDefinition`, or anything CTE-shaped. Constructor (`:16-25`) accepts only `databaseConnectionProvider`, `tableSchemaProvider`, `functionProvider`, `outerTablesInScope`. The visible-tables computation (`:50-51`) is:

```csharp
var tablesInSelect = sqlSelectDefinition.TablesInSelect;
var visibleTables = tablesInSelect.Concat(outerTablesInScope).ToList();
```

`SqlSelectDefinition.TablesInSelect` (`src/Core/LogicalEntities/SqlSelectDefinition.cs:79-95`) returns `Table` + every `Joins[i].Table` — never a CTE.

### 2.2 The CTE branch is execute-time only

`QueryEngine.QueryInternal` checks `sqlSelectDefinition.Ctes.Count > 0` at `:76-77` and delegates to `ExecuteWithCtes` (`src/Core/QueryProcessing/QueryEngine.cs:2027-2045`). Each CTE's SELECT is run, results are wrapped into a per-name `DataTable`, and a `CteTableDataProvider` (`:2092-2127`) decorates the underlying `ITableDataProvider` to serve those tables by name. **All of this is post-resolution.** By the time `ExecuteWithCtes` runs, the parsed `SqlSelectDefinition` has already passed through `ResolveReferences` and either succeeded or failed. The integration test fails *before* `ExecuteWithCtes` is reached — actually it fails late, see §2.3.

### 2.3 The actual failure trace

The `Run` helper in `tests/IntegrationTests/AnsiSqlScenarioTests.cs:44-55` calls `grammar.CreateSelect(node, db, db)` which routes through `SelectStmt.Create(ParseTreeNode, IDatabaseConnectionProvider, ITableSchemaProvider, IFunctionProvider?)` (`src/Core/SelectStmt.cs:266-274`), which calls `sqlSelectDefinition.ResolveReferences(...)`. For SQL `WITH AllOrders AS (SELECT ID, Amount FROM Orders) SELECT ID FROM AllOrders`:

1. The main SELECT's `Table = SqlTable(_, "AllOrders")`.
2. `ResolveTablesDatabase` (`SelectReferenceResolver:30-43`) attempts to set the main table's database. `AllOrders` has no database; default is "Sales". So `Table.DatabaseName = "Sales"`. No error here.
3. `ResolveReferences` (`:48-65`) calls `DetermineTableReferencesOnColumns(tablesInSelect, selectColumnTables)`.
4. The single column `ID` matches `tablesInSelect.Count == 1 && string.IsNullOrEmpty(column.TableName)` (`:121`), so the resolver fires `SelectDefinitionColumns.GetColumns(table /* AllOrders */, tableSchemaProvider)` (`:123`).
5. `tableSchemaProvider` is `InMemoryDatabase` (the integration test's `db`). `GetColumns(SqlTable)` (`tests/IntegrationTests/Infrastructure/InMemoryDatabase.cs:63-71`) calls `ResolveTable(sqlTable)` which returns null because `AllOrders` is not in `dataSet.Tables`. The implementation **throws `KeyNotFoundException`** at `:68`.

So the *actual* failure mode in 2026-05-09 main is not the issue body's "Column 'ID' does not belong to table" string — it is `KeyNotFoundException("Table 'AllOrders' is not registered in database 'Sales'.")`. The integration test at `tests/IntegrationTests/AnsiSqlScenarioTests.cs:142-152` is `[Fact(Skip = ...)]`, and the comment on `Scenario_NonRecursiveCte_ParsesToSqlSelectDefinition` (`:122-140`) corroborates this: "Reference-resolution is intentionally skipped here because the CTE name is not a schema-provider-registered table; ResolveReferences would fault on lookup."

The `Column 'ID' does not belong to table` message is the **secondary symptom** that surfaces if the consumer skips reference resolution and lets execution proceed — execution then hits `ResolveSelectColumnsFromTable` at `src/Core/QueryProcessing/QueryEngine.cs:509-537`. With unresolved column expressions, the row column lookup at `:532` (`dataRow[GetColumnName(sqlColumn.ColumnName)]`) misfires.

**Both symptoms have the same root cause:** the resolver does not understand CTEs, so the parsed AST never reaches the same shape the hand-built tests construct.

## 3. Hand-built vs parsed divergence

Hand-built (`tests/Core.Tests/QueryProcessing/QueryEngineTests.cs:2309-2341`):

```csharp
SqlTable cteTable = new(databaseName, "cte");
sqlSelect.Table = cteTable;
sqlSelect.Columns.Add(new SqlColumn(databaseName, "cte", "Name") { TableRef = cteTable });
sqlSelect.Ctes.Add(new SqlCteDefinition("cte", cteSelect));
// QueryEngine.QueryAsDataTable() runs directly — no ResolveReferences()
```

Parsed (`SelectStmt.AddCtes` `src/Core/SelectStmt.cs:350-377` + `AddTable` `:528-532`):

```csharp
sqlSelectDefinition.Table = TableName!.Create(tableNameNode);    // SqlTable(_, "AllOrders"), no TableRef binding established
sqlSelectDefinition.Columns.Add(SqlColumn { ColumnName = "ID", TableRef = null });
sqlSelectDefinition.Ctes.Add(new SqlCteDefinition("AllOrders", cteSelectDefinition));
// Then ResolveReferences() is called — and faults
```

**Property-level diff at the time the QueryEngine starts executing:**

| Property | Hand-built | Parsed (today) |
|---|---|---|
| `Columns[i].TableRef` | Set to the CTE's `SqlTable` | `null` |
| `Columns[i].ColumnType` | `null` (test happens not to need it) | `null` |
| `InvalidReferenceReason` | `null` (resolver bypassed) | `"…AllOrders is not registered…"` exception |
| `Table.DatabaseName` | `"MyDB"` (set explicitly) | `"Sales"` (set by `ResolveTablesDatabase`) |
| `Ctes[i].SelectDefinition.Columns[j].TableRef` | Set to inner table | `null` (resolver of inner SELECT also doesn't run with CTEs) |

`TableRef` is the load-bearing divergence, but it is symptomatic. The structural divergence is that the parsed path *attempts* resolution and there is no resolver path that succeeds for CTE-named scopes.

## 4. Resolver gap — call site map

`SelectReferenceResolver` is created exclusively from `SqlSelectDefinition.ResolveReferences` (`src/Core/LogicalEntities/SqlSelectDefinition.cs:42-53`), which is invoked from:

| Call site | Passes `outerTablesInScope`? | Notes |
|---|---|---|
| `src/Core/SelectStmt.cs:271` (`SelectStmt.Create` overload with providers) | No | The integration-test path. |
| `src/Core/Stmt.cs:314` (`Stmt.Create` with providers, when statement is a SELECT) | No | The non-grammar-specific stmt entry point. |
| `src/Core/Stmt.cs:318` (INSERT … SELECT) | No | Same shape. |
| `src/Core/InsertStmt.cs:80` | No | Same shape. |
| `SelectReferenceResolver:437` (recursive into `SqlDerivedTable.SelectDefinition`) | No | Calls public overload. |
| `SelectReferenceResolver:443` (EXISTS subquery) | Yes — `visibleTables` of outer | Subquery gets outer FROM tables. |
| `SelectReferenceResolver:465` (scalar subquery) | Yes — `visibleTables` of outer | Same. |

**Observations.**

1. `outerTablesInScope` is already the seam for "things visible from beyond my own FROM/JOIN." Reusing it for CTEs is the lowest-friction shape.
2. The recursion into derived tables (`ResolveDerivedTables`, `:433-439`) does **not** propagate `outerTablesInScope`. That is a latent secondary concern (a derived table cannot reference a correlated outer column today), out of scope for #174 — see §10.
3. Each `SqlSelectDefinition.ResolveReferences` is called once per definition and recursively per nested SELECT (subquery / EXISTS / derived). **No site recursively walks `Ctes` and resolves them, then exposes the CTE as a visible table.** That is the missing pre-pass.

## 5. Fix options

### Option A — Pre-resolve CTEs, expose them as visible tables (recommended)

- **What**: In `SqlSelectDefinition.ResolveReferences` (or in `SelectReferenceResolver` ctor/`ResolveTablesDatabase`), iterate `this.Ctes` in declaration order. For each CTE: (a) resolve the CTE's `SelectDefinition` against the current `outerTablesInScope` plus the *previously-resolved* CTEs in this same WITH; (b) wrap the CTE as an `SqlTable`-shaped object whose schema = the CTE's projected columns; (c) append it to a list of "CTEs visible to the main SELECT," which is then concatenated with `outerTablesInScope` for the main resolve.
- **Carrier type**: easiest is to introduce `SqlCteTable : SqlTable` (CTE name as `TableName`, holds reference to the resolved `SqlSelectDefinition`). `SelectDefinitionColumns.GetColumns` (`src/Core/Utils/SelectDefinitionColumns.cs:9-15`) gets a sibling case `if (table is SqlCteTable c) return GetColumns(c.SelectDefinition);`. This mirrors the existing `SqlDerivedTable` branch precisely.
- **Code touched**: `SelectReferenceResolver.cs` (~30 lines for CTE pre-pass), `SelectDefinitionColumns.cs` (+1 case, ~3 lines), one new file `SqlCteTable.cs`, optional small change in `SqlSelectDefinition.ResolveReferences` to thread CTEs through.
- **Public API impact**: One new public class (`SqlCteTable`) — additive. No signature changes on existing public methods.
- **Correctness**:
  - Single CTE referenced once: `TableRef` falls out of `tablesInSelect` matching `AllOrders` → `SqlCteTable("AllOrders")` in scope.
  - CTE referencing prior CTE: handled by resolving CTEs in declaration order, each seeing `outerTablesInScope ∪ alreadyResolvedCtes`.
  - CTE used multiple times: same CTE table instance is found by name each time.
  - Nested CTEs (`WITH a AS (WITH b AS … SELECT) SELECT FROM a`): the inner WITH resolves with the outer WITH's CTEs already in scope, since the recursion threads them through.
  - Recursive CTE: see §7.
- **Consumer impact**: zero direct (FileBased.DataProviders, MockDB, SettingsOnADO) per §6. They could optionally use `SqlCteTable` later for type-checked dispatch but don't need to today.
- **Tradeoff**: Resolution of the CTE body now happens at parse-resolve time, not execute time. If a CTE body has invalid references, the whole statement fails resolution — which is the correct SQL semantics anyway, so this is positive.

### Option B — Add a separate `ctesInScope` parameter on the resolver

- **What**: Threading a new `IList<SqlCteDefinition> ctesInScope` parameter through the resolver constructor and overloads.
- **Code touched**: every signature of `ResolveReferences` (3 methods on `SqlSelectDefinition`), the resolver ctor, every call site.
- **Public API impact**: 1-2 new public-method overloads. Existing overloads can be preserved.
- **Correctness**: same as A but you keep two parallel "scope" lists in the resolver (tables vs CTEs) and have to write parallel matching logic in `DetermineTableReferencesOnColumns` and `TableFinder`. More code, more places to forget.
- **Tradeoff**: cleaner type discrimination (a CTE is not a `SqlTable`), but the column/table matching code is duplicated. Loses the "CTE is a derived-table-shape" insight.

### Option C — `ITableSchemaProvider` decorator for CTE awareness

- **What**: Wrap the input `tableSchemaProvider` in a `CteAwareTableSchemaProvider` that, for any `SqlTable` whose name matches a CTE, returns the CTE's projected columns. Mirrors how `CteTableDataProvider` decorates `ITableDataProvider` at execute time (`QueryEngine.cs:2092`).
- **Code touched**: One new decorator class, plumbing in `SqlSelectDefinition.ResolveReferences` to wrap the provider.
- **Correctness**: works for the "schema lookup" half. **But** the resolver's `tablesInSelect` set still won't include the CTE — except by accident (the CTE *is* `Table` for `SELECT … FROM AllOrders`, so it's in `tablesInSelect`). Not so for `outerTablesInScope` correlated cases. Also the decorator must call `SelectDefinitionColumns.GetColumns(cte.SelectDefinition)` to get columns, which means the CTE body must already be resolved — circular without Option A's pre-pass.
- **Tradeoff**: parallels execute-time machinery, but doesn't actually solve the structural problem (it just patches schema lookup). Likely needs A anyway.

### Option D — AST rewrite: replace each CTE reference with an inline `SqlDerivedTable`

- **What**: At parse time (or as an AST visitor before resolution), every `SqlTable` whose name matches a CTE is replaced with `SqlDerivedTable(cte.SelectDefinition, alias=cteName)`. The CTEs collection is consumed; the AST becomes "no CTEs, just inlined subqueries."
- **Correctness for non-recursive**: works. Existing derived-table machinery handles it.
- **Correctness for multi-use**: a CTE referenced 3 times becomes 3 separately-resolved derived tables — semantically equivalent to non-materialized CTEs, but doubles/triples the work and *changes execution semantics* if the CTE has side effects (e.g., `random()`). PostgreSQL by default *does* inline CTEs since PG12 ([PostgreSQL 12 release notes](https://www.postgresql.org/docs/12/release-12.html)), but explicit `MATERIALIZED` opt-in is a well-known knob.
- **Correctness for recursive**: cannot be inlined. Needs a different path entirely. Forces #168 to retake the structural decision.
- **Consumer impact**: changes the `SqlSelectDefinition` shape consumers see post-parse. AST-only tooling (the parse-only test at `:122-140` exists for exactly this audience) would observe a different tree.
- **Tradeoff**: largest correctness compromise (semantic and consumer-facing), and it bifurcates the recursive vs non-recursive paths.

## 6. Recommendation: Option A

**Why A.**

1. **Smallest correctness surface.** The hard work is "make the CTE projected columns look like a table schema to the resolver." `SelectDefinitionColumns.GetColumns(SqlSelectDefinition)` *already exists* and *already projects column types*; we just need a `SqlTable` subclass that the helper recognises. This is the same trick `SqlDerivedTable` uses — it's a proven seam.
2. **No public API break.** `SelectReferenceResolver` is `internal`. `SqlSelectDefinition.ResolveReferences` keeps its signature. Only one new public class (`SqlCteTable`).
3. **#168 fits naturally.** A recursive CTE needs the CTE name visible *during resolution of its own body* — Option A gives a structural place to do this: insert the CTE-as-table into `outerTablesInScope` before resolving the recursive UNION's right-hand side, with the schema fixed at the anchor's projection.
4. **Zero downstream consumer impact.** §6.1 confirms.

**Tradeoff accepted**: resolution now eagerly walks CTE bodies. For ill-formed CTEs this surfaces errors at resolve time rather than execute time — a desirable shift. For very large CTE bodies this is ~constant overhead vs the existing parser cost.

### 6.1 Downstream consumer audit

`grep` of `C:/Dev/FileBased.DataProviders/src` and `C:/Dev/MockDB/src` for `SelectReferenceResolver`, `outerTablesInScope`, and `SqlColumn.TableRef`:

- `FileBased.DataProviders`: no matches for any of the three. Only string `TableRef` matches are `lastDataTableRef` (a local in `VirtualDataTableExtensions.cs`) — unrelated.
- `MockDB`: one match, `src/Protocols/MySQL.Protocol/DatabaseData/TableRegistry.cs:46`, which reads `column.TableRef` to look up a registered table. Read-only consumer of an already-resolved AST. Option A preserves this contract: post-resolve, `TableRef` will be set (to a `SqlCteTable` for CTE-bound columns). MockDB needs no change unless it wants to special-case CTE tables.
- `SettingsOnADO`: no matches.

`git -C C:/Dev/FileBased.DataProviders log --since="2026-05-05"`: no output (no commits since Wave 4 audit baseline).
`git -C C:/Dev/MockDB log --since="2026-05-05"`: 9 commits in Wave 1/2 of MockDB's own uber-report — all in MockDB's protocol/server code, none touching SqlBuildingBlocks public surface. Audit holds.

## 7. #168 sequencing

#168 (recursive CTE execution) requires that during resolution of the CTE's `SelectDefinition`, the CTE name itself is bindable in the recursive term. With Option A this is mechanical:

1. Detect `cte.IsRecursive == true`.
2. Resolve the **anchor** (left side of the UNION ALL) first against `outerTablesInScope ∪ priorCtes`. This gives us a projected schema.
3. Build `SqlCteTable(cteName, anchorSchema)` and add it to scope.
4. Resolve the **recursive term** (right side) against `outerTablesInScope ∪ priorCtes ∪ thisCte`. Column references like `org.id` now bind to `thisCte`'s `SqlCteTable`.

Today the recursive case is parsed (`src/Core/SelectStmt.cs:217-221, 360-361`) and the CTE's body is a UNION (anchor + recursive) that becomes `cteSelectDefinition.SetOperations`. Option A's CTE pre-pass needs a small branch for recursive CTEs that resolves the anchor first, registers the CTE table, then resolves the recursive term. This is a ~10-line branch on top of A — does not require redesign.

**Conclusion**: A is forward-compatible with #168. #168's *resolver* work piggybacks on A's pre-pass. #168's *executor* work (anchor + iterate-until-fixed-point) is independent and remains in `QueryEngine.ExecuteCte` — the architect for #168 still needs to design the executor loop, depth limit, and cycle detection (per #168 AC).

## 8. Test plan (for the implementation PR)

- **Un-skip** `Scenario_NonRecursiveCte_ExecutesEndToEnd_NotImplemented` (`tests/IntegrationTests/AnsiSqlScenarioTests.cs:142-152`) and assert end-to-end result rows.
- **Promote** `Scenario_NonRecursiveCte_ParsesToSqlSelectDefinition` (`:122-140`) to also pass `db, db` to `CreateSelect` (today it deliberately does not, per the comment at `:128-130`) — this confirms `ResolveReferences` no longer faults on CTE names.
- **Add** to `tests/Core.Tests/QueryProcessing/QueryEngineTests.cs` (parse-side, CTE-aware resolve):
  - Chained CTEs: `WITH a AS (...), b AS (SELECT * FROM a) SELECT FROM b`.
  - CTE referenced multiple times in main query.
  - CTE inside a JOIN: `SELECT … FROM Customers c JOIN cte ON c.id = cte.id`.
  - CTE with WHERE/JOIN inside its body.
  - CTE-shaped ambiguous column: `WITH a AS (SELECT id FROM T1), b AS (SELECT id FROM T2) SELECT id FROM a, b` should fault on ambiguous column `id` (parity with the existing ambiguity error path at `SelectReferenceResolver:166`).
  - CTE projecting an aliased column: `WITH a AS (SELECT id AS x FROM T) SELECT x FROM a`.
  - CTE resolved against `outerTablesInScope` (correlated): `SELECT (SELECT id FROM cte WHERE …) FROM T` where cte is from the outer WITH.
- **Add** to `tests/IntegrationTests/AnsiSqlScenarioTests.cs`:
  - Parse → resolve → execute round-trip for each of the above shapes the grammar can express.
- **Cross-cutting parity**: per current state of the integration test suite (`MySqlScenarioTests`, `PostgreSqlScenarioTests`, `SqlServerScenarioTests`), each dialect should get an analogous non-recursive CTE end-to-end scenario. AnsiSQL is the lowest common denominator; if any dialect's grammar differs on WITH, file a sub-issue.
- **Hand-built test preservation**: the three existing CTE tests at `tests/Core.Tests/QueryProcessing/QueryEngineTests.cs:2309, 2344, 2398` must continue to pass. They construct `TableRef` directly and bypass resolution — Option A does nothing on the bypass path.
- **Negative**: a CTE whose body is itself ill-formed (`WITH a AS (SELECT badcol FROM T) SELECT * FROM a`) should set `InvalidReferenceReason` at resolve time, not throw.

## 9. Open questions

1. **Carrier type**: introduce `SqlCteTable : SqlTable` vs reuse `SqlDerivedTable` with the CTE name in `TableAlias`? `SqlDerivedTable`'s ctor (`src/Core/LogicalEntities/SqlDerivedTable.cs:5-10`) sets `TableName = alias`. Reuse is tempting but `SqlDerivedTable.TableName` is the alias whereas a CTE has a *real* name (and might still have an outer alias `WITH a AS (...) SELECT * FROM a x` — uncommon but legal in some dialects). Recommend a new `SqlCteTable` to keep semantics distinct. **Architect decision needed.**
2. **CTE column-alias list**: SQL standard allows `WITH cte(x, y) AS (SELECT a, b FROM T)` to rename the projected columns. Today `SelectStmt.AddCtes` (`src/Core/SelectStmt.cs:350-377`) reads only `Id + AS + (...)` — no explicit-column-list grammar production. **Filing this as a follow-up issue is recommended; it is not in #174 AC.**
3. **Case sensitivity of CTE name lookup**: `CteTableDataProvider` uses `OrdinalIgnoreCase` (`QueryEngine.cs:2095`). `SelectReferenceResolver`'s table-name comparison uses `databaseConnectionProvider.CaseInsensitive` (`:381`). Ensure the CTE pre-pass matches the resolver's convention, not the executor's, so that resolve and execute agree.
4. **Latent secondary bug — `ResolveDerivedTables` does not propagate `outerTablesInScope`** (`SelectReferenceResolver:433-439`). A derived table embedded in a SELECT cannot reference a correlated outer column today. Out of scope for #174; recommend filing.
5. **What does the issue body's "Column X does not belong to table" exception come from?** I could not reproduce it from code — the resolver throws first. Possibly the original investigator skipped resolution (matching the parse-only scenario test) and ran the engine, where `ResolveSelectColumnsFromTable:532` would fail because `sqlColumn.ColumnName` is null/empty or the source `dataRow` is from `CteTableDataProvider`'s shape vs the unbound expectation. **Architect should re-validate the exception text under the actual implementation path before implementation.**

## 10. Follow-ups for team lead

- **#168 sequencing is correct**: keep #174 ahead of #168. Document in #168's body that resolver work is now subsumed by #174.
- **File new issue**: explicit-column-list CTE syntax (`WITH cte(x, y) AS (...)`) — grammar production missing. Out of #174 scope.
- **File new issue**: `ResolveDerivedTables` does not propagate `outerTablesInScope` — correlated derived tables silently misresolve. Severity: low (no consumer hits this today), latent.
- **#174 AC update suggested** (return to lead, do not edit issue):
  - Replace the current AC bullet that says "ResolveReferences (or whichever visitor builds CteTableDataProvider) populates SqlColumn.TableRef on the projected columns" with: "`SelectReferenceResolver` is made CTE-aware (per Option A in research doc): each `SqlCteDefinition.SelectDefinition` is resolved in declaration order against prior CTEs and `outerTablesInScope`, then the CTE is surfaced as a CTE-aware `SqlTable` for the main SELECT's resolution, so `SqlColumn.TableRef` is populated by the existing column-resolution pipeline."
  - Keep the existing un-skip-the-test AC and the no-regression-on-hand-built AC unchanged.

---

**Citations index** (file:line, used above):

- `src/Core/Utils/SelectReferenceResolver.cs` — full read; key lines 16-25, 30-43, 48-65, 50-51, 121-140, 166, 381, 433-439, 437, 443, 465, 67-84.
- `src/Core/LogicalEntities/SqlSelectDefinition.cs:10, 39-53, 79-95`.
- `src/Core/LogicalEntities/SqlCteDefinition.cs:1-32`.
- `src/Core/LogicalEntities/SqlDerivedTable.cs:1-15`.
- `src/Core/LogicalEntities/SqlTable.cs:1-73`.
- `src/Core/LogicalEntities/SqlColumn.cs:1-22`.
- `src/Core/SelectStmt.cs:266-274, 350-377, 528-532`.
- `src/Core/Stmt.cs:307-322`.
- `src/Core/QueryProcessing/QueryEngine.cs:76-77, 509-537, 2027-2127`.
- `src/Core/Utils/TableFinder.cs:1-122`.
- `src/Core/Utils/SelectDefinitionColumns.cs:9-15, 23-47`.
- `tests/Core.Tests/QueryProcessing/QueryEngineTests.cs:2309-2440, 3240-3300`.
- `tests/IntegrationTests/AnsiSqlScenarioTests.cs:44-55, 122-152`.
- `tests/IntegrationTests/Infrastructure/AnsiSqlGrammar.cs:14-42`.
- `tests/IntegrationTests/Infrastructure/InMemoryDatabase.cs:63-71, 98-108`.
- `C:/Dev/MockDB/src/Protocols/MySQL.Protocol/DatabaseData/TableRegistry.cs:46`.
