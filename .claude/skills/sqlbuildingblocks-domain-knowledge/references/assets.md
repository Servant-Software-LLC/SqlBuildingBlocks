# SqlBuildingBlocks Asset Inventory

## Core Product Assets

| Asset Area | Observed Asset | Status |
|------------|----------------|--------|
| Core NonTerminal Framework | 20+ building blocks (Stmt, SelectStmt, Expr, Id, etc.) | Production |
| Logical Entities | ~30 semantic entity types (expressions, columns, tables, definitions) | Production |
| AnsiSQL Grammar | SQL-89 base + FETCH FIRST clause | Production |
| MySQL Grammar | Backtick identifiers, LIMIT/OFFSET, INTERVAL, ON DUPLICATE KEY | Production |
| PostgreSQL Grammar | Minimal stub | Under development |
| SQL Server Grammar | Minimal stub | Under development |
| Query Engine | In-memory SELECT execution (CTEs, JOINs, aggregates, window functions) | Active development |
| Visitor Infrastructure | Expression/value visitors, function/parameter resolvers | Production |
| Constraint Definitions | PRIMARY KEY, UNIQUE, FOREIGN KEY, CHECK constraints | Production |
| DDL Support | CREATE/ALTER/DROP TABLE/VIEW/INDEX, RENAME TABLE | Production |
| Transaction Support | BEGIN, COMMIT, ROLLBACK, SAVEPOINT | Production |
| Reference Resolution | Column validation, type inference, alias resolution | Production |

## Test Assets

| Test Type | Projects | Coverage |
|-----------|----------|----------|
| Core Tests | Core.Tests (NonTerminal, logical entity, visitor tests) | Comprehensive |
| AnsiSQL Tests | AnsiSQL.Tests (cross-database ANSI compliance) | Good |
| MySQL Tests | MySQL.Tests (MySQL-specific dialect) | Good |
| PostgreSQL Tests | PostgreSQL.Tests (basic) | Minimal |
| SQL Server Tests | SQLServer.Tests (basic) | Minimal |
| Cross-Cutting Tests | CrossCutting.Tests (all grammars) | Good |

## NuGet Packages (5 total)

| Package | NuGet ID |
|---------|----------|
| Core | SqlBuildingBlocks.Core |
| AnsiSQL Grammar | SqlBuildingBlocks.Grammars.AnsiSQL |
| MySQL Grammar | SqlBuildingBlocks.Grammars.MySQL |
| PostgreSQL Grammar | SqlBuildingBlocks.Grammars.PostgreSQL |
| SQL Server Grammar | SqlBuildingBlocks.Grammars.SQLServer |

## Dependencies

- Irony 1.5.3 (LR parser engine)
- System.ValueTuple (netstandard2.0 compatibility)
- xUnit + FluentAssertions + Moq (testing)
- BenchmarkDotNet (performance testing)
- coverlet (code coverage)
