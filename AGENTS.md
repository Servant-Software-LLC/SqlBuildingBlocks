# AGENTS.md

This file provides guidance to AI coding agents (including OpenAI Codex) when working in this repository.

## Repository Overview

SqlBuildingBlocks is a C# library for parsing, building, and transforming SQL statements. It provides grammar definitions, AST node types, and query construction utilities used by ADO.NET file-based data providers (CSV, JSON, XML, XLS).

## Review Guidelines

When reviewing pull requests, focus on the following by priority:

### P0 — Must fix (security, correctness, crashes)
- SQL injection vectors introduced in grammar rules or string-building utilities
- Parser rules that accept malformed SQL silently (should throw a meaningful parse error)
- Infinite loops or stack overflows in recursive grammar productions
- Incorrect AST node construction that produces wrong SQL on round-trip

### P1 — Should fix (logic bugs, incorrect behavior)
- Grammar regressions: valid SQL that previously parsed now fails
- Operator precedence or associativity errors in expression parsing
- Missing or incorrect handling of SQL keywords (e.g., `NULL`, `IS`, `BETWEEN`, `LIKE`)
- Incorrect column or table alias resolution
- Off-by-one errors in token position tracking

### P2 — Nice to fix (skip unless trivial)
- Grammar rule naming inconsistencies
- Missing unit test coverage for new grammar productions
- Minor performance improvements in the parser hot path

## What to Skip
- Code style and formatting
- Suggestions to restructure the grammar unless they fix a P0/P1 issue
- Refactoring suggestions unrelated to the PR's scope

## Key Interface Contracts

### `ITableDataProvider` vs `IAllTableDataProvider`

`ITableDataProvider` (`src/Core/Interfaces/ITableDataProvider.cs`) is the base data-access
contract: `GetTableData(SqlTable)`, `GetTables(string?)`, and `GetColumns(SqlTable)`. A
provider may serve only a single, fixed table.

`IAllTableDataProvider` (`src/Core/Interfaces/IAllTableDataProvider.cs`) extends
`ITableDataProvider` with no additional members. It is a **semantic marker** meaning: "this
provider can satisfy `GetTableData` for *any* table in the schema." `QueryEngine`'s
multi-table constructor overloads require this type, not `ITableDataProvider`, to enforce that
JOIN execution only happens when the provider can serve every table referenced in the query.

The canonical implementation is `AllTableDataProvider` (`src/Core/Utils/AllTableDataProvider.cs`),
which fans out calls across a DI-injected `IEnumerable<ITableDataProvider>`. External consumers
(e.g. MockDB's `SchemaManagerTableDataProvider`) implement `IAllTableDataProvider` directly when
they hold an in-memory representation of the full schema.

**Do not** add a concrete method to `IAllTableDataProvider` without a careful NuGet-surface review
— it is part of the public API and removing or changing its signature is a breaking change.
