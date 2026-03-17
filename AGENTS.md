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
