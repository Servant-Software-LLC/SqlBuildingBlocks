# SqlBuildingBlocks
[![Build](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/actions/workflows/main.yml/badge.svg)](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/actions/workflows/main.yml)

## Project Status

| Component | Status |
|-----------|--------|
| AnsiSQL grammar | Production-ready — SQL-89 + FETCH FIRST, 86+ negative tests per dialect |
| MySQL grammar | Production-ready — backticks, LIMIT/OFFSET, INTERVAL, ON DUPLICATE KEY UPDATE |
| PostgreSQL grammar | In development (stub — do not use in production) |
| SQL Server grammar | In development (stub — do not use in production) |
| Query engine | Beta — SELECT with CTEs (recursive + non-recursive), JOINs, window functions, aggregates, UNION/INTERSECT/EXCEPT, ORDER BY, GROUP BY/HAVING |

## Overview

SqlBuildingBlocks is an extensible open-source library for parsing SQL into manageable, logical classes tailored to different database technologies. It is built upon Irony's SQLGrammar example and leverages the Factory and Strategy patterns for customization of SQL parsing across multiple databases.

## How It Works

SqlBuildingBlocks breaks down SQL into fundamental "building blocks" — NonTerminal classes, each handling a specific part of the SQL language. These NonTerminal classes use a factory pattern to create logical classes that represent SQL elements.

## Query Engine

The `QueryEngine` class provides in-memory SQL execution. It accepts an `ITableDataProvider` (or `IAllTableDataProvider` for multi-table queries) and a parsed `SqlSelectDefinition`, and returns a `VirtualDataTable` of result rows.

**Supported SELECT features:**
- Simple projections, column aliases, `SELECT *`
- `FROM` table, `INNER`/`LEFT`/`RIGHT`/`FULL OUTER JOIN`
- `WHERE` clause with full predicate support (comparisons, `IS NULL`, `LIKE`, `IN`, `BETWEEN`, logical operators)
- `GROUP BY` + `HAVING` (aggregate predicates)
- `ORDER BY` with `ASC`/`DESC` and `NULLS FIRST`/`NULLS LAST`
- `DISTINCT`
- Aggregate functions: `COUNT`, `SUM`, `AVG`, `MIN`, `MAX` (including `COUNT(DISTINCT col)`)
- Window functions: `ROW_NUMBER`, `RANK`, `DENSE_RANK`, `LAG`, `LEAD`, `NTILE`, `FIRST_VALUE`, `LAST_VALUE`, `NTH_VALUE`; `ROWS BETWEEN` frames; `RANGE BETWEEN INTERVAL` frames (DateTime ORDER BY)
- `WITH` (non-recursive CTE) and `WITH RECURSIVE` CTE; configurable depth limit via `QueryEngineOptions.MaxRecursionDepth` (default 100)
- `UNION`, `UNION ALL`, `INTERSECT`, `EXCEPT`
- Derived tables in `FROM` and `JOIN` positions
- SQL Server `TOP N`
- DBNull / three-valued logic in all predicates

**Not yet supported** (throws `NotSupportedException`): correlated subqueries, `EXISTS`, `ROLLUP`/`CUBE`/`GROUPING SETS`, `GROUP BY ROLLUP`, DML execution (INSERT/UPDATE/DELETE are parse-only).

```csharp
// Parse
var grammar = new AnsiSqlGrammar();
var parser = new Parser(grammar);
var parseTree = parser.Parse("SELECT * FROM Orders WHERE Amount > 100");

// Resolve
var resolver = new SelectReferenceResolver(selectDef, connectionProvider, schemaProvider, functionProvider);
resolver.ResolveTablesDatabase();
resolver.ResolveReferences();

// Execute
var engine = new QueryEngine(tableDataProvider, selectDef);
var result = engine.QueryAsDataTable();
```

## Project Objectives
- **Extensibility**: Specialized grammars for each database technology, extending a shared AnsiSQL base.
- **Usability**: Represent complex SQL grammar as manageable, logical classes.
- **Testability**: Strong unit-testing framework — 1279 passing tests across five NuGet packages.

## Roadmap

- PostgreSQL grammar — dialect extensions beyond the current stub
- SQL Server grammar — dialect extensions beyond the current stub
- Correlated subquery execution
- `GROUP BY ROLLUP` / `CUBE` / `GROUPING SETS` execution

## Contributing
We're open to contributions from the community. Please refer to our [contributing guidelines](CONTRIBUTING.md) for more information.

## License
This project is licensed under the terms of the MIT license. For more information, please see the [LICENSE](LICENSE) file.

## Installation

Install via NuGet. All five packages publish at the same version on every merge to `main`.

| Package Name | Release (NuGet) |
|---|---|
| `SqlBuildingBlocks.Core` | [![NuGet](https://img.shields.io/nuget/v/SqlBuildingBlocks.Core.svg)](https://www.nuget.org/packages/SqlBuildingBlocks.Core/) |
| `SqlBuildingBlocks.Grammars.AnsiSQL` | [![NuGet](https://img.shields.io/nuget/v/SqlBuildingBlocks.Grammars.AnsiSQL.svg)](https://www.nuget.org/packages/SqlBuildingBlocks.Grammars.AnsiSQL/) |
| `SqlBuildingBlocks.Grammars.MySQL` | [![NuGet](https://img.shields.io/nuget/v/SqlBuildingBlocks.Grammars.MySQL.svg)](https://www.nuget.org/packages/SqlBuildingBlocks.Grammars.MySQL/) |
| `SqlBuildingBlocks.Grammars.PostgreSQL` | [![NuGet](https://img.shields.io/nuget/v/SqlBuildingBlocks.Grammars.PostgreSQL.svg)](https://www.nuget.org/packages/SqlBuildingBlocks.Grammars.PostgreSQL/) |
| `SqlBuildingBlocks.Grammars.SQLServer` | [![NuGet](https://img.shields.io/nuget/v/SqlBuildingBlocks.Grammars.SQLServer.svg)](https://www.nuget.org/packages/SqlBuildingBlocks.Grammars.SQLServer/) |

## Contact

For any inquiries or issues, please [open a GitHub issue](https://github.com/Servant-Software-LLC/SqlBuildingBlocks/issues).
