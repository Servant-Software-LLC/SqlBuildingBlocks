---
name: sqlbuildingblocks-domain-knowledge
description: |
  SqlBuildingBlocks product knowledge for Servant Software LLC agents. Use when working on anything
  related to SqlBuildingBlocks:
  - Product strategy, roadmap, or feature planning
  - Technical architecture discussions or documentation
  - SQL grammar design, dialect support, or parser questions
  - Integration with consuming projects (FileBased.DataProviders, MockDB)
  - Understanding current maturity, gaps, or productization needs

  Provides: product purpose, architecture overview, asset inventory, maturity assessment, and productization gaps.

  SELF-UPDATING: When your work changes, advances, or extends SqlBuildingBlocks in ways that affect
  this knowledge (new grammars, features, assets, maturity changes, resolved gaps, etc.), you MUST
  update this skill and its reference files to reflect the new state before completing your task.
  This keeps the knowledge accurate for future agents. Update the specific section(s) affected --
  do not rewrite unchanged content.
---

# SqlBuildingBlocks Domain Knowledge

## Quick Reference

**What is SqlBuildingBlocks?** An extensible open-source library that parses SQL into manageable, logical classes tailored to different database technologies. Built on Irony's SQLGrammar with Factory and Strategy patterns for customization. Provides both grammar-level parsing and an in-memory query engine.

## Core Product Facts

- **Primary Interface**: Grammar classes that parse SQL strings into logical entity graphs
- **Parser Engine**: Irony (LR parser) -- SQL grammar defined as NonTerminal rule compositions
- **Supported Dialects**: AnsiSQL (production), MySQL (production), PostgreSQL (stub), SQL Server (stub)
- **Query Engine**: In-memory execution of parsed SELECT statements against ITableDataProvider
- **Tech Stack**: netstandard2.0, Irony 1.5.3, .NET 10 SDK for builds
- **License**: MIT
- **Distribution**: NuGet (5 packages: Core + 4 grammar packages)
- **Status**: Under active development, not yet production-ready per README

## Key Assets

| Asset | Status |
|-------|--------|
| Core NonTerminal Framework | Production (20+ building blocks) |
| Logical Entity Classes | Production (~30 semantic entity types) |
| AnsiSQL Grammar | Production (SQL-89 + FETCH FIRST) |
| MySQL Grammar | Production (backticks, LIMIT/OFFSET, INTERVAL, ON DUPLICATE KEY) |
| PostgreSQL Grammar | Stub (under development) |
| SQL Server Grammar | Stub (under development) |
| Query Engine | Active development (SELECT with CTEs, JOINs, aggregates, window functions) |
| Visitor Infrastructure | Production (expression/value visitors, function/parameter resolvers) |
| NuGet Distribution | 5 packages published |

## Ecosystem Role

SqlBuildingBlocks is a foundational OSS dependency in Servant Software LLC's product stack:

- **FileBased.DataProviders** -- Uses SqlBuildingBlocks to parse SQL for file-based CRUD operations
- **MockDB** -- Uses SqlBuildingBlocks for SQL parsing in protocol endpoints (MySQL, PostgreSQL, SQL Server)

## Maturity Assessment

| Dimension | Score | Notes |
|-----------|-------|-------|
| Problem/Solution Fit | 4/5 | Clear value for extensible SQL parsing across dialects |
| Core Engineering | 4/5 | Well-structured NonTerminal hierarchy, factory/strategy/visitor patterns |
| Grammar Coverage | 3/5 | AnsiSQL + MySQL solid; PostgreSQL + SQL Server are stubs |
| Query Engine | 2/5 | Basic SELECT execution; many SQL features unsupported |
| Quality & Reliability | 3/5 | Good test coverage for core, cross-cutting tests across grammars |
| Documentation | 2/5 | README overview + AGENTS.md; no API reference or grammar documentation |
| Commercial Readiness | 3/5 | MIT licensed, NuGet published, CI/CD pipeline |

## Top Gaps

### P0 (Critical)
| Gap | Impact |
|-----|--------|
| PostgreSQL grammar is a stub | Limits FileBased.DataProviders and MockDB PostgreSQL support |
| SQL Server grammar is a stub | Limits MockDB SQL Server protocol |
| README states "not yet viable for production use" | Adoption hesitancy |

### P1 (Important)
| Gap | Impact |
|-----|--------|
| Query engine has limited feature coverage | Many SQL operations unsupported at execution level |
| No grammar documentation or SQL compatibility matrix | Hard to know what SQL is supported per dialect |
| No error recovery in parser | Single parse failure = complete failure |

## Detailed References

See [references/](references/) for:
- Architecture Overview
- Asset Inventory
- Maturity Assessment
- Gaps & Next Steps
