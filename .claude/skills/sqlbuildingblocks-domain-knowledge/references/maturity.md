# SqlBuildingBlocks Product Maturity Assessment

## Assessment Scope

SqlBuildingBlocks (MIT licensed, NuGet published) -- Extensible SQL parsing library with Irony-based grammar, logical entity classes, and in-memory query engine.

## Executive Maturity Rating

**Overall: Active development, production-ready for AnsiSQL/MySQL parsing**

| Dimension | Score | Notes |
|-----------|-------|-------|
| Problem/Solution Fit | 4/5 | Clear value for extensible SQL parsing; consumed by FileBased.DataProviders and MockDB |
| Core Engineering | 4/5 | Well-structured NonTerminal hierarchy, factory/strategy/visitor patterns |
| Grammar Coverage | 3/5 | AnsiSQL + MySQL solid; PostgreSQL + SQL Server are stubs |
| Query Engine | 2/5 | Basic SELECT with WHERE, JOINs, aggregates; many features unsupported |
| Quality & Reliability | 3/5 | Good test coverage for core and MySQL; cross-cutting test suite |
| Documentation | 2/5 | README overview + AGENTS.md for AI agents; no API reference or grammar docs |
| Distribution | 4/5 | 5 NuGet packages, CI/CD pipeline with Codecov |

## Evidence Snapshot

- **Architecture**: NonTerminal building blocks → logical entities via factory pattern
- **Stack**: netstandard2.0, Irony 1.5.3, .NET 10 SDK
- **Grammars**: AnsiSQL (SQL-89 + FETCH FIRST), MySQL (backticks, LIMIT/OFFSET, INTERVAL, ON DUPLICATE KEY)
- **Entities**: ~30 logical entity types covering DML (SELECT/INSERT/UPDATE/DELETE), DDL (CREATE/ALTER/DROP), expressions, functions, aggregates
- **Query Engine**: In-memory SELECT execution with CTEs, JOINs, aggregates, window functions, ORDER BY, LIMIT/OFFSET
- **Testing**: 6 test projects (Core, AnsiSQL, MySQL, PostgreSQL, SQL Server, CrossCutting)
- **Distribution**: 5 NuGet packages, CI/CD on GitHub Actions with Codecov

## Strengths

1. Clean grammar → logical entity pipeline via factory pattern
2. Reusable NonTerminal building blocks across dialects
3. Visitor pattern enables expression tree transformation
4. Query engine provides in-memory SQL execution
5. Well-tested core and MySQL grammar

## Key Risks

1. README states "not yet viable for production use" -- despite being consumed by production systems
2. PostgreSQL and SQL Server grammars are stubs
3. Query engine has limited SQL feature support
4. Irony parser has no error recovery
5. No SQL compatibility matrix documenting what's supported per dialect
