# `SqlExpression` Consumer-Pattern Audit

**Issue**: [#157](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/157) — gates [#132](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues/132)
**Date**: 2026-05-05
**Auditor**: /uber-report Wave 1 Lane B (orchestrator-direct grep)

## Purpose

Issue #132 proposes enforcing the discriminated-union shape of `src/Core/LogicalEntities/SqlExpression.cs` (today: 14 mutually-exclusive nullable properties — `BinExpr`, `BetweenExpr`, `CaseExpr`, `CastExpr`, `ExistsExpr`, `Value`, `Parameter`, `Column`, `Function`, `JsonExpr`, `WindowFunction`, `ScalarSubquery`, `InList`, `Unary`).

The implicit precedence baked into `SqlExpression.GetExpression()` (`src/Core/LogicalEntities/SqlExpression.cs:145-204`) is `BinExpr → BetweenExpr → CaseExpr → Value → Function → Parameter → Column` (and so on). The Devil's Advocate agent flagged that downstream consumers may pattern-match on this union, and that a naive enforcement could break them silently. This audit catalogs every consumer call site and recommends an enforcement shape that does not break them.

## Repos audited

| Repo | Path |
|------|------|
| FileBased.DataProviders | `C:/Dev/FileBased.DataProviders` |
| MockDB | `C:/Dev/MockDB` |
| SettingsOnADO | `C:/Dev/SettingsOnADO` |

## Findings

### MockDB: zero touches on `SqlBuildingBlocks.LogicalEntities.SqlExpression`

The only `SqlExpression` mention in MockDB is `Microsoft.EntityFrameworkCore.Query.SqlExpressions` in `src/EFCore.Provider/Query/Internal/MockDBQuerySqlGenerator.cs:1-3` — a different type from EF Core. **MockDB does not consume our `SqlExpression` union at the property level.** It uses other SqlBuildingBlocks types (`SqlSelectDefinition`, `SqlBinaryExpression`, etc.) but does not pattern-match on the discriminated-union arms.

**#132 has zero direct blast radius on MockDB.**

### SettingsOnADO: zero touches

`Grep -r "SqlExpression"` on `C:/Dev/SettingsOnADO/src` returns no matches. **Not a consumer.**

### FileBased.DataProviders: real but contained touches

Three categories of touch, ordered by risk to #132 enforcement:

#### Category 1 — Pass-through property exposure (zero risk)

Files that expose `SqlExpression` as a public property type without inspecting its arms:

| Site | Code | Notes |
|------|------|-------|
| `src/Data.Common/FileStatements/FileUpdate.cs:23` | `public SqlExpression Filter { get; }` | Type-passthrough only |
| `src/Data.Common/FileStatements/FileDelete.cs:22` | `public SqlExpression Filter { get; }` | Type-passthrough only |
| `src/Data.Common/FileStatements/FileInsert.cs:39` | `private void SetValues(IList<SqlExpression> values)` | Type-passthrough only |

These are **immune** to a single-arm enforcement on `SqlExpression`: they just hold the reference.

#### Category 2 — Calling `ToExpressionString()` (zero risk)

Sites that consume `SqlExpression` only via its serialization helper, never via property inspection:

| Site | Code |
|------|------|
| `src/Data.Common/FileIO/Write/FileUpdateWriter.cs:32` | `dataView.RowFilter = fileUpdate.Filter?.ToExpressionString();` |
| `src/Data.Common/FileIO/Delete/FileDeleteWriter.cs:31` | `dataView.RowFilter = query.Filter?.ToExpressionString();` |

`ToExpressionString()` is a method, not a property — its contract is invariant under #132 as long as the implementation continues to honor the existing `GetExpression()` precedence.

#### Category 3 — Pattern-matching on union arms (the real risk)

Three sites inspect the discriminated arms directly, **but always via `SqlAssignment` convenience properties**, not on a raw `SqlExpression`:

**`src/Data.Common/FileIO/Write/FileUpdateWriter.cs:48-57`**

```csharp
foreach (SqlAssignment assignment in fileUpdate.Assignments)
{
    var columnName = assignment.Column.ColumnName;
    dataTable.Columns[columnName]!.ReadOnly = false;

    if (assignment.Value == null)
    {
        var assignmentRight = assignment.Parameter != null
            ? $"{assignment.Parameter}({nameof(assignment.Parameter)})"
            : assignment.Function != null
                ? $"{assignment.Function}({nameof(assignment.Function)})"
                : "Unknown type";
        throw new Exception($"Right side of the assigment did not supply a literal value...");
    }

    dataRow[columnName] = assignment.Value.Value;
}
```

This site reads `.Value`, `.Parameter`, `.Function` on a `SqlAssignment`. **Critically, those `SqlAssignment` properties are convenience pass-throughs to `Expression`** (per `src/Core/LogicalEntities/SqlAssignment.cs:25-28`):

```csharp
public SqlLiteralValue? Value => Expression.Value;
public SqlParameter? Parameter => Expression.Parameter;
public SqlFunction? Function => Expression.Function;
```

So this site **does** pattern-match on the discriminated arms — just one level of indirection away. Today's behavior:
- If parsing yields a `SqlExpression` with `.Value` set, `assignment.Value` is non-null → take the literal path (line 57).
- Otherwise, examine `Parameter` / `Function` → diagnostic message naming the unresolved type.

**`src/Data.Common/FileStatements/FileInsert.cs:44-47`**

```csharp
foreach (SqlExpression value in values)
{
    if (value.Value == null)
        throw new Exception($"SqlExpression value of {value} does not contain a {typeof(SqlLiteralValue)}");
    sqlLiteralValues.Add(value.Value);
}
```

Direct `.Value` inspection on `SqlExpression`. Consumer expects: **at INSERT time, every value expression must be a literal**. The fail path is taken whenever `.Value` is `null` — i.e., when the parser produced a different arm.

## Implicit precedence consumers depend on

From the audit:

| Consumer assumption | What it really tests | What enforcement must preserve |
|---|---|---|
| `assignment.Value != null` ⇒ "right side is a literal" | `Expression.Value != null` ⇒ value-arm semantics | `Value` arm must remain a non-null marker for the literal case |
| `assignment.Parameter != null` ⇒ "right side is parameterized" | `Expression.Parameter != null` ⇒ parameter-arm | `Parameter` arm must remain non-null for parameter case |
| `assignment.Function != null` ⇒ "right side is a function call" | `Expression.Function != null` ⇒ function-arm | `Function` arm must remain non-null for function case |
| `value.Value == null` (`FileInsert`) ⇒ "not a literal — error" | `Expression.Value == null` ⇒ any other arm | Same as above |

**No consumer reads any of the other 11 arms** (`BinExpr`, `BetweenExpr`, `CaseExpr`, `CastExpr`, `ExistsExpr`, `Column`, `JsonExpr`, `WindowFunction`, `ScalarSubquery`, `InList`, `Unary`). Those are inspected only by SqlBuildingBlocks's own visitors, query engine, and `ToExpressionString()`.

## Recommended enforcement shape

Three options surveyed:

### Option A — Sealed subtypes with pattern matching
- Replace `SqlExpression` with a sealed abstract base + 14 sealed subclasses.
- Consumers pattern-match: `if (assignment.Expression is SqlValueExpression v) { ... }`.
- **Cons**: Major binary break. `SqlExpression` is widely consumed as a constructable type with public setters. Removing the property surface would force every consumer to migrate. Three SqlAssignment ctors take typed args — those would need to keep working.
- **Verdict**: Too aggressive for the actual audited consumer surface.

### Option B — Case-discriminator enum + payload object
- Add `public SqlExpressionKind Kind { get; }` and a single `Payload` object.
- All 14 properties become `[Obsolete]` shims that read through `Kind` + `Payload`.
- **Cons**: Doubles the type surface during deprecation; hides the union shape behind ceremony.
- **Verdict**: Workable but cumbersome.

### Option C — `Kind` enum + `internal` setters + invariant assertions (recommended)
- Add `public SqlExpressionKind Kind { get; }` (computed once at construction time, not from the property check ladder).
- Make all 14 setters `internal` (already `private set`, so external mutability is already blocked).
- Add a single `AssertInvariant()` at the end of every public constructor and at the end of `AssumeExpressionLikeness` that throws if `Kind` does not match the single non-null property.
- Keep all 14 public getters intact — consumers still write `assignment.Value`, `value.Function`, etc.
- `GetExpression()` precedence is preserved (it just reads through `Kind` first).
- **Cons**: Doesn't eliminate the multiple-property surface, only enforces correctness at construction.
- **Pros**: **Zero binary break for the audited consumer surface.** `FileUpdateWriter`, `FileInsert`, and the convenience properties on `SqlAssignment` continue to work unchanged. Future evolution (window/JSON/lateral expressions) just adds an enum value + a constructor.

**Recommendation**: ship Option C. The audited consumer surface is tight (3 sites, all in FileBased.DataProviders, all reading 3 of the 14 arms via `SqlAssignment` pass-through), and Option C closes the invariant-violation gap (`AssumeExpressionLikeness` rewriting properties without enforcement) without touching the public surface.

## Sign-off

| Owner | Decision | Date |
|-------|----------|------|
| FileBased.DataProviders | _pending_ | _pending_ |
| MockDB | _N/A — no consumer touches_ | 2026-05-05 |
| SettingsOnADO | _N/A — no consumer touches_ | 2026-05-05 |

FileBased.DataProviders sign-off is required before #132 implementation begins, on Option C specifically. The audit shows the risk is contained: the only enforcement-sensitive sites are `FileUpdateWriter.cs:48-57`, `FileInsert.cs:44-47`, and the `SqlAssignment.cs` convenience properties — all of which Option C preserves.

## Files inspected

- `src/Core/LogicalEntities/SqlExpression.cs` (lines 11-204, 337-382)
- `src/Core/LogicalEntities/SqlAssignment.cs` (full file)
- `C:/Dev/FileBased.DataProviders/src/Data.Common/FileStatements/{FileUpdate,FileDelete,FileInsert}.cs`
- `C:/Dev/FileBased.DataProviders/src/Data.Common/FileIO/Write/FileUpdateWriter.cs`
- `C:/Dev/FileBased.DataProviders/src/Data.Common/FileIO/Delete/FileDeleteWriter.cs`
- `C:/Dev/MockDB/src/EFCore.Provider/Query/Internal/MockDBQuerySqlGenerator.cs`
- `C:/Dev/SettingsOnADO/src/**/*.cs` (zero matches)

Methodology: `Grep` for `SqlExpression` across each consumer repo's `src/`, then property-access grep on the matching files. The full grep output is reproducible via the commands logged in the /uber-report Wave 1 task index (`.claude/tasks/2026-05-05-uber-report-v1.tasks.json`).
