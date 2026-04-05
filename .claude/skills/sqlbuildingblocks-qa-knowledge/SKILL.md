---
name: sqlbuildingblocks-qa-knowledge
description: |
  QA-specific knowledge for testing the SqlBuildingBlocks library. Use when planning test strategies,
  evaluating coverage gaps, writing acceptance criteria, investigating regressions, designing test
  matrices, or assessing release readiness.
  Covers: test inventory, coverage analysis, risk-based testing priorities, SQL grammar test matrix,
  parser correctness verification, query engine testing, edge case catalog, known fragile areas,
  and test environment setup.

  SELF-UPDATING: When your work changes, advances, or extends testing in SqlBuildingBlocks (new test
  projects, coverage changes, discovered edge cases, resolved defects, etc.), you MUST update this
  skill to reflect the new state before completing your task.
---

# SqlBuildingBlocks QA Knowledge

## Test Inventory

### Test Projects (6 total)

| Project | Scope | Type |
|---------|-------|------|
| Core.Tests | NonTerminal building blocks, logical entities, visitors, query engine | Unit |
| AnsiSQL.Tests | ANSI SQL-89 grammar compliance | Grammar |
| MySQL.Tests | MySQL-specific dialect features | Grammar |
| PostgreSQL.Tests | PostgreSQL dialect (minimal) | Grammar |
| SQLServer.Tests | SQL Server dialect (minimal) | Grammar |
| CrossCutting.Tests | Cross-grammar consistency | Grammar |

### Test Utilities (Core.Tests/Utils/)

| Utility | Purpose |
|---------|---------|
| `GrammarParser` | Parse SQL, assert no errors, return ParseTreeNode |
| `TableSchemaProvider` | Mock schema with hardcoded test tables (Customers, Orders, Products) |
| `FakeDatabaseConnectionProvider` | Mock table-to-database mapping |
| `FakeFunctionProvider` | Mock function resolution |

### Test Stack
- **Framework**: xUnit 2.9.3
- **Assertions**: FluentAssertions 6.11.0
- **Mocking**: Moq 4.20.72
- **Coverage**: coverlet 8.0.1 (Codecov integration)
- **Performance**: BenchmarkDotNet 0.13.4 (available, no published baselines)

### Running Tests
```powershell
dotnet test --configuration Release
dotnet test --configuration Release --collect:"XPlat Code Coverage"
```

## SQL Grammar Test Matrix

This is the central QA artifact for SqlBuildingBlocks. Every SQL construct must be verified
against every grammar dialect that claims to support it.

### DML Statement Coverage

| SQL Construct | AnsiSQL | MySQL | PostgreSQL | SQL Server | Notes |
|---------------|---------|-------|------------|------------|-------|
| SELECT (basic) | Y | Y | ? | ? | Core path |
| SELECT DISTINCT | Y | Y | ? | ? | |
| SELECT with aliases | Y | Y | ? | ? | |
| SELECT * | Y | Y | ? | ? | |
| SELECT table.* | Y | Y | ? | ? | |
| WHERE clause | Y | Y | ? | ? | |
| ORDER BY (ASC/DESC) | Y | Y | ? | ? | |
| GROUP BY | Y | Y | ? | ? | |
| HAVING | Y | Y | ? | ? | |
| JOIN (INNER) | Y | Y | ? | ? | |
| JOIN (LEFT/RIGHT/FULL) | Y | Y | ? | ? | |
| CROSS JOIN | Y | Y | ? | ? | |
| Subqueries (scalar) | Y | Y | ? | ? | |
| EXISTS subquery | Y | Y | ? | ? | |
| CTEs (WITH ... AS) | Y | Y | ? | ? | |
| UNION / INTERSECT / EXCEPT | Y | Y | ? | ? | |
| LIMIT / OFFSET | -- | Y | ? | -- | MySQL-specific |
| FETCH FIRST N ROWS | Y | -- | ? | -- | ANSI-specific |
| TOP N | -- | -- | -- | ? | SQL Server-specific |
| INSERT (VALUES) | Y | Y | ? | ? | |
| INSERT (SELECT) | Y | Y | ? | ? | |
| ON DUPLICATE KEY UPDATE | -- | Y | -- | -- | MySQL-specific |
| UPDATE (basic) | Y | Y | ? | ? | |
| UPDATE with JOIN | Y | Y | ? | ? | |
| DELETE (basic) | Y | Y | ? | ? | |
| DELETE with JOIN | Y | Y | ? | ? | |
| RETURNING clause | Y | Y | ? | ? | |

**? = PostgreSQL/SQL Server are stubs -- coverage is minimal or unknown**

### DDL Statement Coverage

| SQL Construct | AnsiSQL | MySQL | PostgreSQL | SQL Server |
|---------------|---------|-------|------------|------------|
| CREATE TABLE | Y | Y | ? | ? |
| CREATE VIEW | Y | Y | ? | ? |
| CREATE INDEX | Y | Y | ? | ? |
| ALTER TABLE ADD COLUMN | Y | Y | ? | ? |
| ALTER TABLE DROP COLUMN | Y | Y | ? | ? |
| ALTER TABLE MODIFY COLUMN | Y | Y | ? | ? |
| DROP TABLE | Y | Y | ? | ? |
| DROP VIEW | Y | Y | ? | ? |
| DROP INDEX | Y | Y | ? | ? |
| RENAME TABLE | Y | Y | ? | ? |

### Expression Coverage

| Expression Type | Tested | Notes |
|-----------------|--------|-------|
| Binary operators (=, <>, <, >, <=, >=) | Y | Core.Tests/ExprTests |
| Arithmetic (+, -, *, /) | Y | |
| LIKE / NOT LIKE | Y | |
| IN (list) | Y | |
| IN (subquery) | Y | |
| BETWEEN ... AND | Y | |
| IS NULL / IS NOT NULL | Y | |
| AND / OR / NOT | Y | |
| CASE WHEN / THEN / ELSE | Y | |
| CAST(expr AS type) | Y | |
| EXISTS (subquery) | Y | |
| Scalar subquery | Y | |
| Function calls | Y | |
| Aggregate functions (COUNT, SUM, AVG, MIN, MAX) | Y | |
| Window functions | Y | |
| String literals | Y | |
| Numeric literals (int, float) | Y | |
| NULL literal | Y | |
| Parameterized values (@param) | Y | |
| INTERVAL expressions | MySQL only | MySQL-specific |

### Identifier Quoting

| Style | Grammar | Tested |
|-------|---------|--------|
| Double quotes `"id"` | AnsiSQL | Y |
| Backticks `` `id` `` | MySQL | Y |
| Square brackets `[id]` | SQL Server | ? |
| No quoting | All | Y |

## Risk-Based Testing Priorities

### P0 -- Parser Correctness Risks (from AGENTS.md)

1. **SQL injection vectors in grammar rules**
   - Grammar accepts strings that could cause injection when reconstructed
   - Test: Parse known SQL injection payloads, verify AST represents them literally
   - Test: Ensure no string concatenation in grammar output path

2. **Parser accepts malformed SQL silently**
   - Irony parser may produce a "valid" tree for invalid SQL
   - Test: Feed known-bad SQL, verify ParseTree.HasErrors() returns true
   - Test: Incomplete statements (missing WHERE value, unclosed parentheses)
   - Test: SQL with syntax errors in various positions

3. **Infinite loops or stack overflows in recursion**
   - Deeply nested expressions: `((((((a + b) + c) + d) ...)))`
   - Deeply nested subqueries: `SELECT * FROM (SELECT * FROM (SELECT ...))`
   - Test: Nest expressions 100+ levels deep, verify no stack overflow
   - Test: Verify parser terminates on adversarial input

4. **Incorrect AST construction**
   - Parse SQL, build logical entities, verify each field is correct
   - Operator precedence: `a + b * c` should parse as `a + (b * c)`
   - Associativity: `a - b - c` should parse as `(a - b) - c`
   - Test: Complex expressions with mixed precedence, verify tree structure

### P1 -- Grammar Regression Risks

5. **Previously-parsed SQL now fails**
   - Grammar changes can break existing SQL that was valid
   - Test: Maintain a regression suite of known-good SQL per dialect
   - Test: Run cross-cutting tests on every grammar change

6. **Operator precedence/associativity errors**
   - Test: `NOT a AND b OR c` -- verify grouping
   - Test: `a * b + c / d` -- verify arithmetic precedence
   - Test: Unary minus: `-a + b` vs `-(a + b)`

7. **Alias resolution correctness**
   - Column aliases in ORDER BY / HAVING
   - Table aliases in JOIN conditions
   - Ambiguous column references (same name in multiple tables)
   - Test: SELECT with alias, ORDER BY alias -- verify resolution

8. **Keyword collision**
   - Column/table names that are SQL keywords (e.g., `SELECT`, `ORDER`, `GROUP`)
   - Test: Use reserved words as identifiers (quoted and unquoted)

### P2 -- Query Engine Risks

9. **Query engine produces wrong results**
   - WHERE filtering returns wrong rows
   - JOIN produces incorrect combinations
   - Aggregate functions compute wrong values
   - ORDER BY sorts incorrectly
   - Test: Known-result queries against fixed test data

10. **Query engine throws on valid SQL**
    - Unsupported features throw NotImplementedException
    - Test: Document which SQL features the engine supports vs rejects
    - Test: Verify engine throws clear errors (not generic exceptions)

## Edge Case Catalog

### Parser Edge Cases
- Empty string input
- Whitespace-only input
- Single semicolon `;`
- Multiple statements separated by `;`
- SQL with only comments (`-- comment` or `/* comment */`)
- Very long SQL string (>100KB)
- SQL with Unicode identifiers
- SQL with mixed-case keywords (`SeLeCt`, `FROM`)
- SQL with excessive whitespace/newlines
- SQL with tab characters in identifiers

### Expression Edge Cases
- Deeply nested parentheses: `((((1))))`
- Empty IN list: `x IN ()`
- BETWEEN with reversed bounds: `x BETWEEN 10 AND 1`
- CASE with no ELSE clause
- CAST to unsupported type
- Function call with zero arguments
- Function call with very many arguments (20+)
- Aggregate with DISTINCT: `COUNT(DISTINCT x)`
- Window function with empty OVER clause

### DDL Edge Cases
- CREATE TABLE with zero columns
- CREATE TABLE with duplicate column names
- Column with very long name
- Data type with precision/scale: `DECIMAL(18,2)`, `VARCHAR(MAX)`
- Multiple constraints on same column
- FOREIGN KEY referencing non-existent table (parser doesn't validate)
- CREATE TABLE IF NOT EXISTS

### MySQL-Specific Edge Cases
- Backtick-quoted identifier containing backtick
- LIMIT without OFFSET
- LIMIT 0
- ON DUPLICATE KEY UPDATE with all columns
- INTERVAL with various units (DAY, MONTH, YEAR, HOUR, etc.)

### Cross-Dialect Edge Cases
- Same SQL parsed by different grammars -- do they produce equivalent ASTs?
- ANSI SQL that MySQL extends (e.g., GROUP BY with non-aggregated columns)
- Dialect-specific keywords used as identifiers in other dialects

## Known Fragile Areas

1. **Irony ambiguity** -- LR parser conflicts when grammar rules are ambiguous. Adding a new
   production can cause shift/reduce or reduce/reduce conflicts that manifest as parse failures
   on previously-valid SQL.

2. **Expression recursion depth** -- SqlExpression.Create() is recursive. Very deeply nested
   expressions can overflow the stack. No depth limit is enforced.

3. **Lazy SqlColumnRef resolution** -- Column references are resolved lazily during
   ResolveReferences(). If schema providers return incorrect data, column resolution silently
   produces wrong types or fails cryptically.

4. **Grammar NonTerminal naming** -- Create() methods match on `expression.Term.Name` string
   comparisons. Renaming a NonTerminal without updating all Create() switch cases causes
   silent AST construction failures.

5. **PostgreSQL/SQL Server stubs** -- These grammars exist but support minimal SQL. Using them
   with complex queries will fail. Consumers (FileBased.DataProviders, MockDB) may hit these
   limitations at runtime.

## Test Environment Setup

### Prerequisites
- .NET 10 SDK (see global.json: 10.0.100 with latestFeature rollForward)
- No external services required (all in-memory parsing)

### Test Isolation
- Each test creates its own Grammar instance
- GrammarParser.Parse() is stateless (new Parser per call)
- Mock providers (TableSchemaProvider, etc.) are per-test

### Test Data Patterns
- **Simple parse tests**: Single SQL statement → verify AST structure
- **Round-trip tests**: Parse → build logical entities → verify field values
- **Negative tests**: Invalid SQL → verify parse error
- **Cross-cutting tests**: Same SQL across multiple grammars → verify consistency

## Coverage Gaps to Investigate

1. **PostgreSQL grammar** -- Stub with minimal tests; major gap for consumers
2. **SQL Server grammar** -- Stub with minimal tests; major gap for MockDB TDS protocol
3. **Query engine** -- Limited feature coverage; many SQL operations throw NotImplementedException
4. **Negative testing** -- Insufficient tests for malformed/invalid SQL rejection
5. **Performance baselines** -- BenchmarkDotNet available but no published results
6. **Fuzz testing** -- No randomized/generative SQL testing for parser robustness
7. **Round-trip verification** -- No tests that parse SQL, reconstruct it, and re-parse
8. **Error message quality** -- No tests verifying parse error messages are useful
9. **Cross-platform** -- CI runs on ubuntu-latest only

## Release Readiness Checklist

- [ ] All test projects pass (`dotnet test --configuration Release`)
- [ ] Code coverage meets threshold (Codecov thresholds)
- [ ] No Irony grammar errors (LanguageData validation passes)
- [ ] Cross-cutting tests pass (same SQL across all grammars)
- [ ] No new TreatWarningsAsErrors violations
- [ ] Regression suite of known-good SQL passes per dialect
- [ ] NuGet packages build successfully (`dotnet pack --configuration Release`)
- [ ] Version updated via UpdateVersion.ps1
- [ ] AGENTS.md review priorities addressed (P0 items verified)
