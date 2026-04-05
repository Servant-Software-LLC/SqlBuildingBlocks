# SqlBuildingBlocks Architecture Overview

## Architectural Posture

SqlBuildingBlocks is an extensible SQL parsing library that transforms SQL strings into strongly-typed logical entity graphs. Built on Irony's LR parser, it uses NonTerminal building blocks composed via BNF rules, with Factory and Strategy patterns enabling database-dialect customization. Includes an in-memory query engine for executing parsed SELECT statements against data providers.

## Primary Architectural Building Blocks

### Grammar Layer (Irony-based)
- **SqlGrammar**: Base grammar subclass registering terminals (keywords, literals, identifiers) and composing NonTerminal rules using BNF operators.
- **NonTerminal Classes**: 20+ building blocks (Stmt, SelectStmt, Expr, Id, TableName, JoinChainOpt, WhereClauseOpt, etc.) each implementing `Create(ParseTreeNode)` factory methods.
- **Dialect Grammars**: AnsiSQL (base), MySQL (backticks, LIMIT/OFFSET, INTERVAL), PostgreSQL (stub), SQL Server (stub).

### Logical Entity Layer
- **SqlDefinition**: Top-level discriminated union wrapping all statement types.
- **Statement Definitions**: SqlSelectDefinition, SqlInsertDefinition, SqlUpdateDefinition, SqlDeleteDefinition, SqlCreateTableDefinition, SqlAlterTableDefinition, SqlDropTableDefinition, etc.
- **Expression Entities**: SqlExpression (union container), SqlBinaryExpression, SqlBetweenExpression, SqlCaseExpression, SqlCastExpression, SqlExistsExpression, SqlScalarSubqueryExpression.
- **Column/Table Entities**: SqlColumn, SqlColumnRef (lazy resolution), SqlAllColumns, SqlTable, SqlDataType.
- **Function Entities**: SqlFunction, SqlAggregate, SqlFunctionColumn.

### Query Engine Layer
- **QueryEngine**: Executes SqlSelectDefinition against ITableDataProvider. Supports CTEs, WHERE filtering, GROUP BY/HAVING, aggregates, window functions, ORDER BY, LIMIT/OFFSET.
- **VirtualDataTable**: Streaming result representation for memory-efficient large result sets.
- **Expression-to-LINQ**: SqlBinaryExpression.BuildExpression() converts expression trees to LINQ Expression<Func<T, bool>> predicates.

### Visitor Layer
- **ISqlExpressionVisitor**: Traverses expression tree nodes.
- **ISqlValueVisitor**: Visits leaf value nodes.
- **ResolveFunctionsVisitor**: Resolves function references in expression trees.
- **ResolveParametersVisitor**: Replaces parameter placeholders with literal values.

### Reference Resolution
- **IDatabaseConnectionProvider**: Maps table names to database connections.
- **ITableSchemaProvider**: Provides column types and ordering for validation.
- **IFunctionProvider / ISqlFunctionProvider**: Resolves function signatures and implementations.

## Design Patterns

| Pattern | Usage |
|---------|-------|
| Factory | Each NonTerminal's `Create(ParseTreeNode)` method; Stmt dispatches to statement factories |
| Strategy | ITableDataProvider, ITableSchemaProvider, IFunctionProvider for pluggable behavior |
| Visitor | ISqlExpressionVisitor/ISqlValueVisitor for expression tree traversal |
| Template Method | NonTerminal base with grammar-specific overrides per dialect |
| Builder/Fluent | Irony BnfExpression operator overloads (`\|`, `+`) for grammar rule composition |
| Discriminated Union | SqlExpression and SqlDefinition as type-safe containers for variant types |

## Parsing Pipeline

```
SQL String → Irony LR Parser → ParseTree → NonTerminal.Create() → Logical Entities → Optional ResolveReferences()
```

1. Grammar defines rules via BNF compositions
2. Irony Parser produces ParseTree with ParseTreeNode hierarchy
3. NonTerminal.Create() recursively builds logical entity graph
4. ResolveReferences() validates against schema/function providers

## Ecosystem Dependencies

- **Irony** (1.5.3) -- LR parser engine
- **System.ValueTuple** -- Tuple support for netstandard2.0
- **xUnit + FluentAssertions + Moq** -- Testing
- **BenchmarkDotNet** -- Performance testing

## Strengths

1. Clean separation between grammar rules and semantic entities
2. Extensible via dialect-specific grammar subclasses
3. Factory pattern enables polymorphic AST construction
4. Visitor pattern enables expression tree transformation without modifying entities
5. Query engine enables in-memory SQL execution
6. Shared NonTerminal building blocks reduce grammar duplication across dialects

## Architecture Risks

1. PostgreSQL and SQL Server grammars are stubs -- major gaps
2. Query engine has limited SQL feature support
3. Irony parser has no error recovery (single failure = parse failure)
4. Complex grammar rules can cause ambiguity/conflicts in LR parsing
5. netstandard2.0 target limits modern C# feature usage
