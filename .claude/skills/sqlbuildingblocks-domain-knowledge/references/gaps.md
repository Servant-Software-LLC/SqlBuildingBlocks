# SqlBuildingBlocks Productization Gaps & Recommended Next Steps

## Top Productization Gaps

### P0 (Critical)

| Gap Area | Current Signal | Impact |
|----------|----------------|--------|
| **PostgreSQL Grammar** | Stub implementation | Limits FileBased.DataProviders and MockDB PostgreSQL protocol support |
| **SQL Server Grammar** | Stub implementation | Limits MockDB SQL Server TDS protocol |
| **README Production Status** | States "not yet viable for production use" | Adoption hesitancy despite actual production consumption |

### P1 (Important)

| Gap Area | Current Signal | Impact |
|----------|----------------|--------|
| **Query Engine Coverage** | Basic SELECT only; many SQL features throw NotImplementedException | Limits in-memory execution scenarios |
| **Grammar Documentation** | No SQL compatibility matrix per dialect | Consumers cannot determine supported SQL subset |
| **Error Recovery** | Irony parser fails entirely on first error | Poor developer experience for SQL authoring |
| **API Documentation** | No reference docs beyond README + AGENTS.md | Developer onboarding friction |

### P2 (Nice to Have)

| Gap Area | Current Signal | Impact |
|----------|----------------|--------|
| **Performance Benchmarks** | BenchmarkDotNet dependency exists but no published results | No baseline for parse/execute performance |
| **SQL Pretty-Printing** | No reverse transformation (logical entities → SQL string) | Limits tooling use cases |
| **Error Messages** | Parse failures return Irony's generic messages | Hard to diagnose grammar issues |

## Recommended Next Steps

### Workstream A -- Grammar Completion
1. Implement PostgreSQL grammar (RETURNING, arrays, JSON operators, PostgreSQL types)
2. Implement SQL Server grammar (TOP, OUTPUT, MERGE, table hints)
3. Create SQL compatibility matrix documenting supported features per dialect
4. Update README to reflect actual production status

### Workstream B -- Query Engine
1. Expand query engine feature coverage (subqueries, UNION, complex JOINs)
2. Add execution plan logging for debugging
3. Document supported vs unsupported SQL at execution level

### Workstream C -- Developer Experience
1. Generate API reference documentation
2. Improve error messages with source position and context
3. Create dialect migration guides (AnsiSQL → MySQL patterns)
4. Add SQL pretty-printing / round-trip capability

### Workstream D -- Quality
1. Publish performance benchmarks
2. Add fuzz testing for grammar robustness
3. Expand cross-cutting tests for all grammar combinations
