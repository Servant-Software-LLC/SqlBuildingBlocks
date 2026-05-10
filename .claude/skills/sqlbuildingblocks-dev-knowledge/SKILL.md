---
name: sqlbuildingblocks-dev-knowledge
description: |
  Developer-specific knowledge for working in the SqlBuildingBlocks codebase. Use when implementing
  features, fixing bugs, adding grammar rules, extending logical entities, writing tests, or navigating
  the project structure. Covers: solution layout, Irony grammar patterns, NonTerminal hierarchy,
  logical entity classes, query engine, visitor pattern, grammar-specific dialects, testing patterns,
  and practical workflows.

  SELF-UPDATING: When your work changes, advances, or extends SqlBuildingBlocks in ways that affect
  this knowledge (new NonTerminals, logical entities, grammar rules, query engine features, etc.), you
  MUST update this skill to reflect the new state before completing your task. This keeps the knowledge
  accurate for future agents. Update the specific section(s) affected -- do not rewrite unchanged content.
---

# SqlBuildingBlocks Developer Knowledge

## Solution Layout

```
src/
  Core/                         -- Core library: NonTerminals, logical entities, query engine, visitors
  Grammars/
    AnsiSQL/                    -- ANSI SQL-89 grammar (base dialect)
    MySQL/                      -- MySQL dialect (backtick identifiers, LIMIT/OFFSET, INTERVAL)
    PostgreSQL/                 -- PostgreSQL dialect (stub, under development)
    SQLServer/                  -- SQL Server dialect (stub, under development)

tests/
  Core.Tests/                   -- NonTerminal, logical entity, visitor, and utility tests
  Grammars/
    AnsiSQL.Tests/              -- ANSI SQL compliance tests
    MySQL.Tests/                -- MySQL-specific tests
    PostgreSQL.Tests/           -- PostgreSQL-specific tests
    SQLServer.Tests/            -- SQL Server-specific tests
    CrossCutting.Tests/         -- Cross-grammar tests
  IntegrationTests/             -- End-to-end synthetic ITableDataProvider scenarios

benchmarks/
  SqlBuildingBlocks.Benchmarks/ -- BenchmarkDotNet suite (parser + QueryEngine hot paths)

Docs/
  Architectural/                -- Recorded architectural decisions
  Benchmarks/                   -- Captured baselines + run/compare instructions
```

## Build & SDK

- **SDK**: .NET 10.0.100 (see global.json, rollForward: latestFeature) with MSBuild Traversal 3.0.2
- **Source TFMs**: netstandard2.0
- **Test TFMs**: net10.0
- **Benchmarks TFM**: net10.0 (uses `$(NetTFM)`)
- **Language**: C# 11 with nullable enabled, TreatWarningsAsErrors: true
- **Package pins**: Packages.props centralizes versions (Irony 1.5.3, xUnit 2.9.3, Moq 4.20.72, BenchmarkDotNet 0.13.4)

### Running Tests
```powershell
# Build
dotnet build --configuration Release

# All tests
dotnet test --configuration Release

# Benchmarks (ShortRun for local dev, omit --job for full statistical run)
dotnet run --configuration Release --project benchmarks/SqlBuildingBlocks.Benchmarks -- --filter "*" --job short
```

The benchmark project is excluded from `dotnet test` (it contains no xUnit tests).
Captured baselines and the regression-threshold rule (10% mean / 20% allocated bytes)
live in `Docs/Benchmarks/README.md`.

## How SQL Parsing Works

### Pipeline: SQL String → Logical Objects

```
1. SQL String
   ↓
2. Irony Parser (LR parsing via Grammar + LanguageData)
   ↓
3. ParseTree / ParseTreeNode hierarchy
   ↓
4. NonTerminal.Create(ParseTreeNode) methods  [factory pattern]
   ↓
5. Logical entity graph (SqlSelectDefinition, SqlExpression, etc.)
   ↓
6. Optional: ResolveReferences() with schema/function providers
```

### Step-by-step

1. **Grammar definition**: SqlGrammar subclass registers terminals (keywords, literals, identifiers) and builds NonTerminal rules using Irony's BNF operators (`|`, `+`)
2. **Parsing**: Irony's Parser executes LR parsing, producing a ParseTree
3. **AST construction**: Each NonTerminal class implements `Create(ParseTreeNode)` that recursively builds logical entity objects
4. **Reference resolution** (optional): `ResolveReferences()` validates column references, infers types, resolves aliases using IDatabaseConnectionProvider, ITableSchemaProvider, IFunctionProvider. CTEs are pre-resolved in declaration order by `SelectReferenceResolver.ResolveCtes()`: each CTE body resolves against prior CTEs (surfaced as `SqlCteTable` instances in `outerTablesInScope`), and main-SELECT FROM/JOIN tables whose names match a CTE are rewritten in place to `SqlCteTable` so the rest of the resolver treats them like derived tables.

## NonTerminal Hierarchy (Grammar Building Blocks)

### Statement-Level

| NonTerminal | Purpose |
|-------------|---------|
| `Stmt` | Root dispatcher -- factory for SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, TRANSACTION, SAVEPOINT |
| `SelectStmt` | SELECT with CTEs, joins, aggregates, window functions, set operations |
| `InsertStmt` | INSERT with VALUES or SELECT sources |
| `UpdateStmt` | UPDATE with FROM, WHERE, RETURNING clauses |
| `DeleteStmt` | DELETE with WHERE, RETURNING, joins |

### Expression-Level

| NonTerminal | Purpose |
|-------------|---------|
| `Expr` | Expression building block -- binary, unary, CASE, CAST, EXISTS, scalar subqueries, BETWEEN, IN |
| `Id` | Identifiers (schema.table.column hierarchies) |
| `SimpleId` | Basic identifier token |
| `Parameter` | Parameterized query placeholders |
| `LiteralValue` | String, numeric, NULL, date literals (see "Numeric literal runtime types" below) |
| `FuncCall` | SQL function calls with arguments |
| `DataType` | Type definitions (INT, VARCHAR, etc.) |

### Compositional Building Blocks

| NonTerminal | Purpose |
|-------------|---------|
| `TableName` | Table references with optional aliases |
| `JoinChainOpt` | JOIN chains (INNER, LEFT, RIGHT, FULL, CROSS) |
| `WhereClauseOpt` | WHERE clause filtering |
| `OrderByList` | ORDER BY with ASC/DESC |
| `AliasOpt` | Optional aliases |
| `IdList` / `ExprList` | Comma-separated lists |
| `Comment` | SQL comments |

### DDL Building Blocks

| NonTerminal | Purpose |
|-------------|---------|
| `CreateTableStmt` | CREATE TABLE with columns and constraints |
| `CreateViewStmt` | CREATE VIEW |
| `CreateIndexStmt` | CREATE INDEX |
| `AlterStmt` | ALTER TABLE (ADD, DROP, MODIFY columns) |
| `DropTableStmt` / `DropViewStmt` / `DropIndexStmt` | DROP statements |
| `RenameTableStmt` | RENAME TABLE |
| `TransactionStmt` | BEGIN, COMMIT, ROLLBACK |
| `SavepointStmt` | SAVEPOINT handling |

### Adding a New NonTerminal

1. **Create class** inheriting from Irony's `NonTerminal` in `src/Core/`
2. **Define grammar rule** in constructor using BNF: `this.Rule = term1 | term2 | (term3 + term4)`
3. **Implement `Create(ParseTreeNode)` factory method** returning the logical entity
4. **Wire into parent** -- add to Stmt, SelectStmt, or appropriate parent's Rule
5. **Create corresponding logical entity** in `src/Core/LogicalEntities/` if needed
6. **Write tests** in `tests/Core.Tests/` using GrammarParser utility

### Reserving Keywords (avoid silent IdentifierTerminal capture)

`grammar.ToTerm("FOO")` registers a keyword token but does **not** prevent the
`IdentifierTerminal` from also matching `FOO` in identifier-position contexts.
When a malformed SQL pattern leaves the parser expecting an identifier
(e.g. a trailing comma in a SELECT column list, leaving `FROM` in
columnSource position), the lexer happily produces `FROM` as an identifier and
the grammar silently accepts the input.

The fix is `grammar.MarkReservedWords("FROM", "INTO", ...)` near the place the
keyword is introduced. Existing examples in the codebase:

- `SelectStmt.cs` reserves `FROM`, `INTO` (issue #167 -- the dangling SELECT comma fix)
- `Expr.cs` reserves `CASE`, `WHEN`, `THEN`, `ELSE`, `END`, `CAST`, `IS`, `BETWEEN`, `EXISTS`
- `LiteralValue.cs` reserves `NULL`, `TRUE`, `FALSE` and synonyms

### Numeric literal runtime types (#184)

`LiteralValue` configures Irony's `NumberLiteral` with `DefaultFloatType = TypeCode.Decimal`,
so unsuffixed fractional literals (e.g. `3.00`, `12.345`) materialize as `System.Decimal`,
not `System.Double`. SQL numerics are exact, not approximate, and decimal preserves the
literal's scale. Integer literals materialize as `System.Int32` (Irony's
`DefaultIntTypes = [TypeCode.Int32]`). Scientific-notation literals (e.g. `3.0e2`) still
go through Irony's exponent path and may produce `System.Double`.

`LiteralValue.Create` accepts every numeric runtime type the grammar can hand it:
`int`, `decimal`, `double`, `float`, plus `string` and the boolean / NULL terms. If you
add a new numeric path that materializes some other CLR type (e.g. `long` for an
unsuffixed-integer overflow case), extend both `Create`'s switch and `SqlLiteralValue`'s
constructors / `GetExpression` arms — the discriminated union must cover every type.
- `MergeStmt.cs` reserves `MERGE`, `USING`, `MATCHED`, `SOURCE`, `TARGET`
- Various dialect-specific keywords reserved in their respective files

Rule of thumb: **if a keyword's misuse should be a syntax error rather than a column reference, reserve it.** When you add or modify a NonTerminal that introduces a new SQL keyword, decide whether it should be reserved. Then add a NegativeTests row that proves the malformed pattern is rejected.

## Logical Entity Classes (src/Core/LogicalEntities/)

These represent the semantic meaning of parsed SQL:

### Expression Entities
- `SqlExpression` -- Union-type container for all expression kinds
- `SqlBinaryExpression` -- Binary operations (=, <>, >, <, LIKE, IN, AND, OR, etc.)
- `SqlBetweenExpression` -- BETWEEN ... AND ...
- `SqlCaseExpression` -- CASE WHEN/THEN/ELSE/END
- `SqlCastExpression` -- CAST(expr AS type)
- `SqlExistsExpression` -- EXISTS (SELECT ...)
- `SqlScalarSubqueryExpression` -- Scalar subqueries
- `SqlInList` -- IN (value1, value2, ...)

### Column & Table Entities
- `SqlColumn` -- Physical column definition
- `SqlColumnRef` -- Column reference with lazy resolution
- `SqlAllColumns` -- SELECT * or SELECT table.*
- `SqlTable` -- Table with database, name, optional alias
- `SqlDerivedTable` -- Inline subquery in FROM/JOIN position (`SqlTable` subclass carrying a `SqlSelectDefinition`)
- `SqlCteTable` -- CTE reference in FROM/JOIN position (`SqlTable` subclass carrying the resolved CTE's `SqlSelectDefinition`); created by `SelectReferenceResolver` during the CTE pre-pass, not at parse time
- `SqlDataType` -- SQL type specification

### Function Entities
- `SqlFunction` -- Function calls with arguments
- `SqlAggregate` -- COUNT, SUM, AVG, MIN, MAX, STDEV, VAR
- `SqlFunctionColumn` -- Column produced by a function

### Statement Definitions
- `SqlDefinition` -- Top-level discriminated union for all statement types
- `SqlSelectDefinition` -- Complete SELECT model (CTEs, columns, FROM, JOINs, WHERE, GROUP BY, HAVING, ORDER BY, LIMIT/OFFSET, TOP, set operations, window functions)
- `SqlInsertDefinition` -- INSERT with columns and values/SELECT
- `SqlUpdateDefinition` -- UPDATE with assignments, WHERE, joins, RETURNING
- `SqlDeleteDefinition` -- DELETE with WHERE, joins, RETURNING

### DDL Definitions
- `SqlCreateTableDefinition`, `SqlCreateViewDefinition`, `SqlAlterTableDefinition`
- `SqlDropTableDefinition`, `SqlRenameTableDefinition`
- `SqlCreateIndexDefinition`, `SqlDropIndexDefinition`

## Grammar Dialects

### AnsiSQL (base grammar)
- SQL-89 standard + FETCH FIRST N ROWS clause
- Entry point: `SqlGrammar` in `src/Grammars/AnsiSQL/`
- All core NonTerminals instantiated here

### MySQL
- Backtick-quoted identifiers via `MySqlTerminalFactory`
- Custom `SimpleId` for MySQL identifier rules
- Custom `Expr` with `AddIntervalSupport()` for INTERVAL expressions
- LIMIT/OFFSET syntax
- ON DUPLICATE KEY UPDATE support

### PostgreSQL (stub)
- Minimal implementation, under development
- Planned: RETURNING clause, array types, JSON operators, PostgreSQL-specific data types

### SQL Server (stub)
- Minimal implementation, under development
- Planned: TOP clause, OUTPUT clause, MERGE statement, table hints

### Extending a Grammar

1. **Subclass SqlGrammar** in `src/Grammars/[Dialect]/`
2. **Override NonTerminals** that need dialect-specific behavior (e.g., custom Expr, SelectStmt)
3. **Use custom TerminalFactory** for dialect-specific identifier quoting
4. **Add dialect-specific productions** to the grammar rules
5. **Write tests** in `tests/Grammars/[Dialect].Tests/`

### Window-frame INTERVAL bounds (issue #180, Wave 14b lane A)

`SelectStmt.windowFrameBound` accepts SQL:2003 `INTERVAL '<magnitude>' <qualifier> PRECEDING|FOLLOWING`
in addition to UNBOUNDED PRECEDING/FOLLOWING, CURRENT ROW, and numeric (row-count) offsets.
The interval magnitude is a single-quoted decimal string; the qualifier is a single-field unit
(YEAR/MONTH/DAY/HOUR/MINUTE/SECOND). All dialects that inherit the core `SelectStmt` accept this
syntax automatically — that means AnsiSQL, MySQL, PostgreSQL.

`SqlBuildingBlocks.Grammars.SQLServer.SelectStmt.CreateWindowFrameBound` overrides the base method
to throw `NotSupportedException` when an INTERVAL bound is encountered. This is the "clean parse
error" contract for SQL Server (which does not support RANGE INTERVAL frames in T-SQL): the
grammar layer accepts the input syntactically, but AST construction surfaces a clear error.

When you add a new dialect that should also reject INTERVAL bounds, override
`CreateWindowFrameBound` in the same way SQL Server does. When you add a dialect that wants its
own window-frame bound shape, override `CreateWindowFrameBound` to dispatch on the new shape.

**Cross-dialect token sharing gotcha**: the `INTERVAL` token is shared across grammars via
`grammar.ToTerm("INTERVAL")` (Irony deduplicates). MySQL's `Expr.AddIntervalSupport` calls
`grammar.MarkPunctuation(INTERVAL)`, which strips INTERVAL from ALL parse-tree child lists
including `intervalFrameLiteral`. `SelectStmt.CreateIntervalLiteral` therefore locates its children
by NonTerminal/term name, not by index — do the same if you add a new INTERVAL-shaped rule.

## Query Engine (src/Core/QueryProcessing/)

In-memory SQL execution engine:

```csharp
public class QueryEngine : IQueryEngine
{
    // Accepts ITableDataProvider + SqlSelectDefinition (+ optional QueryEngineOptions)
    // Returns VirtualDataTable (streaming results)
    
    // Execution order:
    // 1. Validate features (throw on unsupported)
    // 2. Handle CTEs (non-recursive: ExecuteCte; recursive: ExecuteRecursiveCte)
    // 3. Determine columns
    // 4. Get FROM table rows
    // 5. Apply WHERE filtering
    // 6. Handle aggregates/GROUP BY
    // 7. Apply window functions
    // 8. Apply ORDER BY
    // 9. Apply LIMIT/OFFSET
}
```

### Recursive CTE execution (issue #168)

`QueryEngine.ExecuteCte` dispatches to `ExecuteRecursiveCte` when
`SqlCteDefinition.IsRecursive` is true:
- Runs the anchor (the CTE body excluding its `SetOperations` tail) once.
- Loops: replaces `CteTableDataProvider`'s entry for the CTE name with the previous
  iteration's working set, runs the recursive term (`SetOperations[0].Right`),
  appends new rows to a cumulative result, sets the working set to the new rows.
- Terminates when the working set is empty (natural termination) or when the
  iteration counter exceeds `QueryEngineOptions.MaxRecursionDepth` (typed
  `SqlExecutionException` whose message names the CTE and the depth).
- Rejects `UNION` (deduplicating) in the recursive term with `NotSupportedException`
  per the recursive-CTE-semantics ADR; only `UNION ALL` is supported in v1.

### `QueryEngineOptions`

Public, additive options class (issue #168). One knob today:
`MaxRecursionDepth` (validated `>= 1`, default 100, no unlimited sentinel).
All `QueryEngine` constructors accept an optional `QueryEngineOptions`; the
parameterless overloads continue to work for existing consumers.

### CTE name case-sensitivity contract (issue #185, Wave 14b)

`CteTableDataProvider.GetTableData` matches CTE names **case-sensitively**
(`StringComparer.Ordinal`). Case-rules are applied earlier in the pipeline:
`SelectReferenceResolver.ResolveCtes` consults
`DatabaseConnectionProvider.CaseInsensitive` when binding `SqlCteTable`
references and writes back the canonical declared CTE name to
`SqlCteTable.TableName`. By the time the executor looks up a CTE, an exact
string match is correct.

This means a CTE named `chain` does **not** shadow a base table named
`Chain` during execution — the base-table FROM reference is left unbound
to the CTE by the resolver and the executor falls through to the inner
`ITableDataProvider`. Hand-built tests that bypass the resolver must
ensure the `SqlTable.TableName` they put in `FROM` is either the exact
declared CTE name (to hit the CTE) or any other string (to hit the base
provider).

### Non-recursive CTE SetOperation resolution (issue #187, Wave 14b)

When a non-recursive CTE body uses `UNION` / `UNION ALL` / `INTERSECT` /
`EXCEPT`, the right-hand SELECT of each `SqlSetOperation` must have its
column→table references resolved at resolution time. `SelectReferenceResolver.ResolveCtes`
calls `ResolveNonRecursiveSetOperationTerms` immediately after resolving the
primary CTE body, threading the same outer scope (caller's outer scope plus
prior CTEs in this WITH clause). The CTE itself is **not** in scope — a
non-recursive CTE cannot reference itself; only the recursive arm
(`ResolveRecursiveTerms`) adds the CTE to the scope for the recursive term.

Without this, parser-driven CTE bodies that used set operations would reach
the QueryEngine with unresolved column refs on the right-hand SELECT and fail
mid-execution. Hand-built ASTs that pre-wired `TableRef` happened to dodge it.

### Correlated derived tables — outer scope threading (issue #182, Wave 14b)

`SelectReferenceResolver.ResolveDerivedTables` now threads the parent's
outer scope plus the parent's other FROM/JOIN tables (excluding the derived
table itself) into each derived table's inner `ResolveReferences` call. This
makes derived tables effectively **LATERAL** by default — pragmatic for the
in-memory query engine and consistent with how `EXISTS` / scalar-subquery
scope is propagated.

Inner-scope precedence: if a column name is unique to either the inner FROM
or the threaded outer scope, it resolves cleanly. If it appears in both,
the existing `TableFinder` concat-scope flow (`HuntForPossibleTable`)
flags it as ambiguous via the `InvalidReferenceReason` channel — same
semantics as today's EXISTS/scalar subqueries. Resolution errors inside a
derived table propagate to the parent's `InvalidReferenceReason`.

Note: as of Wave 14b the in-memory `QueryEngine` still does not materialize
`SqlDerivedTable` for execution — `AllTableDataProvider.GetTableData` cannot
locate `(<subquery>) AS dt`. Tests for derived-table resolution should use
the parser → `CreateSelect` flow and assert on `selectDefinition.InvalidReferences`
rather than `Run()`-ing the result.

### Self-join correctness — `SqlTableOccurrenceComparer` (issue #183, Wave 13)

`SqlTable.Equals(SqlTable?)` compares `(DatabaseName, TableName)` and **ignores**
`TableAlias`. That contract is part of the public API (consumers may rely on the
alias-blind semantics for "is this column referencing the Customers table?"
checks), so it is unchanged. But the QueryEngine bookkeeping dictionaries must
keep distinct occurrences of the same table — e.g. `t a` and `t b` in a
self-join — separate, otherwise both columns appear to reference the same join
table and the predicate-builder produces a Cartesian product.

`SqlBuildingBlocks.LogicalEntities.SqlTableOccurrenceComparer` (public,
`Instance` static singleton) compares
`(DatabaseName, TableName, TableAlias)` case-insensitively. It is wired into:

- `ProcessingState.TablesInProcessing` / `TablesProjections` /
  `DataRowsOfOtherTables` / `AllJoinTableRows` / `MatchedJoinRows`.
- The local `HashSet<SqlTable>` instances in `QueryEngine.ResolveSelectColumns`
  (RIGHT/FULL OUTER null-padding) and `BuildResultRow` and `GetColumnRefs`.
- The `!=` reference checks in `SqlExpression.GetColumnExpression` — replaced
  with `!SqlTableOccurrenceComparer.Instance.Equals(...)` so the substitute-value
  branch correctly distinguishes a self-join's two occurrences.

Adding a new bookkeeping collection that is keyed by `SqlTable`? Pass
`SqlTableOccurrenceComparer.Instance` to the constructor — otherwise self-joins
will silently collapse into the wrong shape.

### `WhereApplied` reset between FROM-row iterations (issue #183 finding, Wave 13)

`ProcessingState.WhereApplied` tracks whether the WHERE clause has been
incorporated into the JOIN-side filter. The check at
`GetQueryableRowsInJoinTable` only AND's the WHERE into the filter when
`WhereApplied is null`. Pre-Wave 13, `GetQueryableRowsInJoinTable` had a stray
unconditional `processingState.WhereApplied = sqlSelectDefinition.Table;` at the
end that (a) clobbered the more-precise marker the conditional set inside the
`if`, and (b) ran on every iteration of the outer FROM-row loop. After the first
FROM row, `WhereApplied` was non-null forever and subsequent rows skipped
re-incorporating the multi-table WHERE — silently dropping correctness for any
self-join (or other multi-table) WHERE filter.

The fix:
- Removed the bogus unconditional assignment.
- Snapshot `WhereApplied` at the start of `ResolveSelectColumns(processingState,
  fromDataRows)` and reset it to that snapshot at the start of every FROM-row
  iteration. The JOIN-side filter is rebuilt per FROM row anyway (substituted
  values change), so the WHERE-application decision must also be made fresh.

### DBNull / Nullable promotion in comparison predicates (issue #186, Wave 13)

Pre-Wave 13, `SqlExpression.GetExpressionForDataRow` emitted
`Expression.Convert(rawObject, columnType)` (e.g. `Expression.Convert(value, int)`).
At runtime, when the underlying DataRow column held `DBNull.Value`, the conversion
threw — making `JOIN ON nullable_col = literal` and `WHERE nullable_col = X`
unusable for any column that could be NULL. Wave 11's recursive-CTE work had to
use `parent_id = 0` as a "no-parent" sentinel because of this.

The fix lives in two places in `src/Core/LogicalEntities/SqlExpression.cs`:

- `GetExpressionForDataRow` — for value-type columns, the raw indexer value is
  wrapped in a runtime `Expression.Condition(isDBNull, default(T?), (T?)(T)value)`
  that produces `Nullable<T>`. The existing nullable-aware lifting in
  `SqlBinaryExpression.GetBinaryExpression` then produces the correct SQL
  three-valued logic result (UNKNOWN/false) for the predicate.
- `GetColumnExpression` substitute-values branch — when the substituted value is
  `DBNull.Value`, returns `Expression.Constant(null, Nullable<T>)`. When the value
  is non-null, returns `Expression.Constant(typedValue, Nullable<T>)` so the operand
  types align with the DataRow-backed side.

This required fixing a latent companion bug in `GetBinaryExpression`'s
"both-nullable" branch: it used `Expression.Add(leftHasValue, rightHasValue)`
which throws because `Add` is undefined for `bool`. Replaced with
`Expression.AndAlso` plus an unwrapped value comparison so the lifted bool issue
is avoided.

`IS NULL` / `IS NOT NULL` are unchanged — they go through `GetIsNullExpression`,
which reads the raw DataRow indexer directly and compares to `DBNull.Value`.

Operator coverage: `=`, `<>`, `<`, `<=`, `>`, `>=` all flow through
`GetBinaryExpression` and benefit from the fix. `IS NULL` / `IS NOT NULL` had
their own DBNull-aware path already.

### Generic dispatch — `CompiledQueryDispatch` (issue #129, Wave 12)

`QueryEngine` cannot know the element type of an `IQueryable<TDataRow>` at compile
time (a consumer may back a table with `IQueryable<DataRow>`, `IQueryable<MyPoco>`,
etc.). The original implementation routed through `ReflectionHelper.CallMethod`
which used `MethodInfo.MakeGenericMethod` + `MethodInfo.Invoke` per call.

Wave 12 of `/uber-report 2026-05-09` replaced this with
`SqlBuildingBlocks.Utils.CompiledQueryDispatch` (`internal static`):

- Two `ConcurrentDictionary<Type, Delegate>` caches — one for the `ApplyFilter`
  shape, one for `ToDataRows`.
- On first call for a given `TDataRow`, an `Expression.Lambda<...>(...)` is built
  that closes over the open-generic `BuildExpression`, `Queryable.Where`, and
  `IQueryableExtensions.ToDataRows` resolved from `MethodInfo.MakeGenericMethod`,
  then `.Compile()`'d.
- Subsequent calls hit the cached delegate directly.
- `ReflectionHelper` is `[Obsolete]` (kept on the public surface for back-compat)
  and has zero internal callers; all four call sites in
  `QueryEngine.GetQueryableRowsInFromTable` / `GetQueryableRowsInJoinTable` /
  `GetAllRowsInJoinTable` now route through `CompiledQueryDispatch`.

Why this matters when adding new generic dispatch: do **not** re-introduce
`ReflectionHelper.CallMethod` on the QueryEngine hot path. Build a new compiled
delegate cache in the same shape if you need a third dispatch site.

The `InternalsVisibleTo("SqlBuildingBlocks.Core.Tests")` declaration in
`src/Core/SqlBuildingBlocks.Core.csproj` exists so tests can assert on the
internal cache counters (`ApplyFilterCacheCount`, `ToDataRowsCacheCount`,
`ApplyFilterCachedPredicateCacheCount`, `ClearForTests()`).

#### Wave 14 (issue #188) — predicate-shape cache for the JOIN hot path

Wave 14 lane D added a second-tier cache on top of `CompiledQueryDispatch`:
`SqlBuildingBlocks.Utils.CompiledPredicateCache` (also `internal static`). Wave 12
eliminated the per-call `MethodInfo.Invoke` but the per-call
`Expression.Lambda<>(...).Compile()` inside
`SqlBinaryExpression.BuildExpression<TDataRow>` survived as the next bottleneck —
profiling showed `ExecuteJoinedSelect` was spending most of its 240 ms recompiling
the same JOIN-ON predicate 100 times across the FROM-row cross product.

Key changes:
- `SqlBinaryExpression.BuildCompiledPredicate<TDataRow>(SqlTable tableDataRow)` —
  returns a `Func<TDataRow, Dictionary<SqlTable, DataRow>?, bool>` whose body
  reads substitute-table column values from the dictionary at runtime instead of
  baking them as `Expression.Constant`.
- `SqlBinaryExpression.IsCacheableShape()` — gates the cache. Returns true only
  for trees built from `Column`, `Value`, and nested `BinExpr` arms (the only
  arms that thread the runtime-substitute parameter through their `GetExpression`
  overloads). BETWEEN/CASE/IN/NOT IN/Function fall back to the legacy
  `BuildExpression<TDataRow>` path.
- `SqlExpression.GetExpression(..., ParameterExpression? substituteValuesParam)`
  internal overload — when the param is non-null, `GetColumnExpression` emits IL
  that does `dict[tableRef][columnName]` with runtime DBNull/null handling
  (mirroring the Nullable<T> promotion the constant-emission path does at build
  time). When the param is null, the historical constant-baking path runs.
- `CompiledPredicateCache` cache key — a STRUCTURAL FINGERPRINT STRING
  (alias-aware, includes literal values) plus `(typeof(TDataRow), tableDataRow)`.
  The hash alone is NOT sufficient — hash collisions would otherwise return the
  wrong cached lambda. Two predicates with identical shape but different literal
  values hit different slots because the cached lambda still bakes the literal as
  `Expression.Constant` (Approach 2 from the issue plan).
- `CompiledQueryDispatch.ApplyFilter` — now branches on
  `filteringClause.IsCacheableShape()`. Cacheable shapes route through
  `_applyFilterCachedPredicateCache` and use
  `Enumerable.Where(IEnumerable<T>, Func<T, bool>)` instead of
  `Queryable.Where(IQueryable<T>, Expression<Func<T,bool>>)` — the latter
  re-compiles the supplied expression per call when the underlying provider is
  the default LINQ-to-Objects `EnumerableQuery<T>`.

Result: `ExecuteJoinedSelect` 240 ms → 7.5 ms (~32x faster), well under the
issue-AC <100 ms target. `ExecuteSimpleSelect` is unchanged — it has no JOIN
cross product so there was nothing to amortize. Allocation on
`ExecuteJoinedSelect` went up 2.7x (per-call closure capturing substituteValues);
that is acceptable for the speed win and noted in `Docs/Benchmarks/README.md`
as a future optimization opportunity.

When extending the cached path to a new `SqlExpression` arm:
1. Update `SqlBinaryExpression.IsCacheableShape()`'s `IsCacheableArm` switch.
2. Thread `ParameterExpression? substituteValuesParam` through that arm's
   `GetExpression` overload (look at how `BetweenExpr`/`CaseExpr` are *NOT* yet
   threaded — they fall back to the legacy path).
3. Update `CompiledPredicateCache.AppendExpression` to append a stable
   discriminator for the new arm so two predicates differing only in this arm
   produce different fingerprints.
4. Add a regression test in `tests/Core.Tests/Utils/CompiledPredicateCacheTests.cs`
   that exercises both shapes through the QueryEngine and asserts correct results.

### Key Interfaces for Query Engine

| Interface | Purpose |
|-----------|---------|
| `IQueryEngine` | Execute parsed SELECT against data |
| `ITableDataProvider` | Provide rows for a single table |
| `IAllTableDataProvider` | Provide rows for all tables (JOIN support) |
| `ITableSchemaProvider` | Provide column types and ordering |
| `IDatabaseConnectionProvider` | Map table names to databases |
| `IFunctionProvider` / `ISqlFunctionProvider` | Resolve function implementations |

## Visitor Pattern (src/Core/Visitors/)

- `ISqlExpressionVisitor` -- Visit expression tree nodes
- `ISqlValueVisitor` -- Visit leaf value nodes
- `ResolveFunctionsVisitor` -- Traverse and resolve function references
- `ResolveParametersVisitor` -- Replace parameters with literal values
- `WindowFunctionPositionValidator` (internal static) -- Enforces SQL:2003 §7.11; throws
  `SqlExecutionException` if a window function appears at the immediate predicate level
  of WHERE / HAVING / JOIN ON. Walks `SqlExpression` manually (does NOT use
  `ISqlExpressionVisitor.Accept`) so it does not recurse into nested SELECT bodies --
  scalar subqueries that themselves contain window functions in their SELECT list are
  legal. Invoked by `QueryEngine.ThrowIfUnsupportedFeatures()`.

## Testing Patterns

### Test Utilities (tests/Core.Tests/Utils/)

| Utility | Purpose |
|---------|---------|
| `GrammarParser` | Parse SQL string, assert no errors, return ParseTreeNode |
| `TableSchemaProvider` | Mock ITableSchemaProvider with hardcoded test schemas |
| `FakeDatabaseConnectionProvider` | Mock IDatabaseConnectionProvider |
| `FakeFunctionProvider` | Mock IFunctionProvider |
| `AstComparer` | Structural AST equality with diff-style failure reports — emits `path / expected / actual / reason` so a divergence points at a specific property. Used by `RoundTripTests`. |

### Round-trip Tests (`tests/Core.Tests/RoundTripTests.cs`)

Issue #176 safety net for the P0 concern "incorrect AST construction that produces wrong
SQL on round-trip" (AGENTS.md). Two oracles in one file:

- **Expression round-trip** (full): `parse → ToExpressionString → re-parse → AST equality`.
  Uses the existing partial expression renderer.
- **Statement round-trip** (degraded): `parse → re-parse → AST equality`. No statement-level
  renderer exists yet, so this catches grammar non-determinism only.

When you add a new NonTerminal or modify expression rendering, add a round-trip test for
the new construct in this file. When a statement-level renderer is added, swap the
degraded oracle for the full one in `AssertStmtRoundTrip`.

### Standard Test Pattern
```csharp
[Fact]
public void ColumnAndIntLiteral_LessThan()
{
    TestGrammar grammar = new();
    var node = GrammarParser.Parse(grammar, "ID < 3");
    var expression = grammar.Create(node);

    Assert.NotNull(expression.BinExpr);
    Assert.Equal(SqlBinaryOperator.LessThan, expression.BinExpr.Operator);
    Assert.Equal(3, expression.BinExpr.Right.Value.Int);
}
```

### Test Stack
- **xUnit** for test framework (assertions via `Assert.*`)
- **Moq** for mocking schema/function providers
- **coverlet** for code coverage
- **BenchmarkDotNet** for performance testing

### Test Organization
- Individual NonTerminal tests (Expr, Id, SelectStmt, etc.)
- Logical entity tests (SqlBinaryExpression, SqlSelectDefinition, etc.)
- Grammar-specific dialect tests
- Cross-cutting tests across all grammars

## CI/CD

- **Trigger**: Push/PR to main, manual dispatch
- **Runner**: ubuntu-latest, .NET 10.0.x
- **Version**: 1.0.0.${{github.run_number}} (via UpdateVersion.ps1)
- **Packages published to NuGet.org**: Core, Grammars.AnsiSQL, Grammars.MySQL, Grammars.PostgreSQL, Grammars.SQLServer
- **Coverage**: Codecov integration, PR coverage comments

## Review Priorities (from AGENTS.md)

### P0 (Critical)
- SQL injection vectors in grammar rules or string builders
- Parser accepts malformed SQL silently (should throw)
- Infinite loops or stack overflows in recursion
- Incorrect AST construction on round-trip

### P1 (Should Fix)
- Grammar regressions (previously parsed now fails)
- Operator precedence/associativity errors
- Missing/incorrect SQL keyword handling (NULL, IS, BETWEEN, LIKE)
- Incorrect column/table alias resolution
- Token position tracking errors
