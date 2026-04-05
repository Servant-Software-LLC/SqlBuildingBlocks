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
```

## Build & SDK

- **SDK**: .NET 10.0.100 (see global.json, rollForward: latestFeature) with MSBuild Traversal 3.0.2
- **Source TFMs**: netstandard2.0
- **Test TFMs**: net10.0
- **Language**: C# 11 with nullable enabled, TreatWarningsAsErrors: true
- **Package pins**: Packages.props centralizes versions (Irony 1.5.3, xUnit 2.9.3, FluentAssertions 6.11.0, Moq 4.20.72)

### Running Tests
```powershell
# Build
dotnet build --configuration Release

# All tests
dotnet test --configuration Release
```

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
4. **Reference resolution** (optional): `ResolveReferences()` validates column references, infers types, resolves aliases using IDatabaseConnectionProvider, ITableSchemaProvider, IFunctionProvider

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
| `LiteralValue` | String, numeric, NULL, date literals |
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

## Query Engine (src/Core/QueryProcessing/)

In-memory SQL execution engine:

```csharp
public class QueryEngine : IQueryEngine
{
    // Accepts ITableDataProvider + SqlSelectDefinition
    // Returns VirtualDataTable (streaming results)
    
    // Execution order:
    // 1. Validate features (throw on unsupported)
    // 2. Handle CTEs
    // 3. Determine columns
    // 4. Get FROM table rows
    // 5. Apply WHERE filtering
    // 6. Handle aggregates/GROUP BY
    // 7. Apply window functions
    // 8. Apply ORDER BY
    // 9. Apply LIMIT/OFFSET
}
```

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

## Testing Patterns

### Test Utilities (tests/Core.Tests/Utils/)

| Utility | Purpose |
|---------|---------|
| `GrammarParser` | Parse SQL string, assert no errors, return ParseTreeNode |
| `TableSchemaProvider` | Mock ITableSchemaProvider with hardcoded test schemas |
| `FakeDatabaseConnectionProvider` | Mock IDatabaseConnectionProvider |
| `FakeFunctionProvider` | Mock IFunctionProvider |

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
- **xUnit** for test framework
- **FluentAssertions** for assertions
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
