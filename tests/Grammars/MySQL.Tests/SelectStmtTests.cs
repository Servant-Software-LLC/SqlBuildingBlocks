using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.MySQL.Tests;

public class SelectStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            //MySQL has special naming rules for identifiers.  (Note: the backtick)
            //REF: https://dev.mysql.com/doc/refman/8.0/en/identifiers.html
            MySQL.SimpleId simpleId = new(this);

            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            MySQL.Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            MySQL.SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);
            expr.AddIntervalSupport(this);

            Root = selectStmt;
        }

        public virtual SqlSelectDefinition Create(ParseTreeNode selectStmt) =>
            ((SelectStmt)Root).Create(selectStmt);

        public virtual SqlSelectDefinition Create(ParseTreeNode selectStmt, IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider) =>
            ((SelectStmt)Root).Create(selectStmt, databaseConnectionProvider, tableSchemaProvider);
    }

    [Fact]
    public void Select_WithSimpleLimit_NoProvidersForCreateMethod()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT * FROM Customers LIMIT 1");

        var selectStmt = grammar.Create(node);

        //Assert on select columns
        Assert.Single(selectStmt.Columns);

        //First column - *
        Assert.IsType<SqlAllColumns>(selectStmt.Columns[0]);

        //Many assertions are not done on the joins, because JoinChainOptTests.MultipleInnerJoins_WithColumnId_Expressions already thoroughly covers them.
        Assert.Empty(selectStmt.Joins);

        //WHERE
        Assert.Null(selectStmt.WhereClause);

        //LIMIT
        Assert.NotNull(selectStmt.Limit);
        Assert.Equal(0, selectStmt.Limit.RowOffset.Value);
        Assert.Equal(1, selectStmt.Limit.RowCount.Value);
    }


    [Fact]
    public void Select_WithLimit()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT ID, CustomerName FROM Customers LIMIT 5, 10");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert on select columns
        Assert.Equal(2, selectStmt.Columns.Count);

        //First column - ID
        Assert.IsType<SqlColumn>(selectStmt.Columns[0]);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("ID", firstColumn.ColumnName);
        Assert.NotNull(firstColumn.TableRef);
        Assert.Equal("Customers", firstColumn.TableRef.TableName);

        //Second column - CustomerName
        Assert.IsType<SqlColumn>(selectStmt.Columns[1]);
        var secondColumn = selectStmt.Columns[1] as SqlColumn;
        Assert.Equal("CustomerName", secondColumn.ColumnName);
        Assert.NotNull(secondColumn.TableRef);
        Assert.Equal("Customers", secondColumn.TableRef.TableName);

        //Many assertions are not done on the joins, because JoinChainOptTests.MultipleInnerJoins_WithColumnId_Expressions already thoroughly covers them.
        Assert.Empty(selectStmt.Joins);

        //WHERE
        Assert.Null(selectStmt.WhereClause);

        //LIMIT
        Assert.NotNull(selectStmt.Limit);
        Assert.Equal(5, selectStmt.Limit.RowOffset.Value);
        Assert.Equal(10, selectStmt.Limit.RowCount.Value);
    }

    [Fact]
    public void Select_WithLimitAndOffsetKeyword()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT ID, CustomerName FROM Customers LIMIT 10 OFFSET 5");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //LIMIT
        Assert.NotNull(selectStmt.Limit);
        Assert.Equal(5, selectStmt.Limit.RowOffset.Value);
        Assert.Equal(10, selectStmt.Limit.RowCount.Value);
    }

    [Fact]
    public void Select_WithLimitZero()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT * FROM Customers LIMIT 0");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.Limit);
        Assert.Equal(0, selectStmt.Limit.RowCount.Value);
        Assert.Equal(0, selectStmt.Limit.RowOffset.Value);
    }

    [Fact]
    public void Select_ColumnAlias_With_Backticks()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT Name `First Name` FROM Products");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert on select columns
        Assert.Single(selectStmt.Columns);

        //First column - Name `First Name`
        Assert.IsType<SqlColumn>(selectStmt.Columns[0]);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("Name", firstColumn.ColumnName);
        Assert.NotNull(firstColumn.TableRef);
        Assert.Equal("Products", firstColumn.TableRef.TableName);
        Assert.Equal("First Name", firstColumn.ColumnAlias);


        //No JOINs
        Assert.Empty(selectStmt.Joins);

        //WHERE
        Assert.Null(selectStmt.WhereClause);

        //LIMIT
        Assert.Null(selectStmt.Limit);
    }

    [Fact]
    public void Select_BacktickQuoted_ColumnNames()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT `CustomerName` FROM Customers");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.Single(selectStmt.Columns);
        Assert.IsType<SqlColumn>(selectStmt.Columns[0]);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("CustomerName", firstColumn.ColumnName);
        Assert.NotNull(firstColumn.TableRef);
        Assert.Equal("Customers", firstColumn.TableRef.TableName);
    }

    [Fact]
    public void Select_BacktickQuoted_TableName()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT CustomerName FROM `Customers`");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.Single(selectStmt.Columns);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("CustomerName", firstColumn.ColumnName);
        Assert.Equal("Customers", selectStmt.Table.TableName);
    }

    [Fact]
    public void Select_BacktickQuoted_ColumnsAndTable()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT `ID`, `CustomerName` FROM `Customers`");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.Equal(2, selectStmt.Columns.Count);

        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("ID", firstColumn.ColumnName);

        var secondColumn = selectStmt.Columns[1] as SqlColumn;
        Assert.Equal("CustomerName", secondColumn.ColumnName);

        Assert.Equal("Customers", selectStmt.Table.TableName);
    }

    [Fact]
    public void Select_BacktickQuoted_DotSeparatedIdentifiers()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT `c`.`CustomerName` FROM `Customers` c");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("CustomerName", firstColumn.ColumnName);
        Assert.Equal("c", firstColumn.TableName);
        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.Equal("c", selectStmt.Table.TableAlias);
    }

    [Fact]
    public void Select_BacktickQuoted_TableAlias()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT `c`.`ID` FROM `Customers` AS `c`");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("ID", firstColumn.ColumnName);
        Assert.Equal("c", firstColumn.TableName);
        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.Equal("c", selectStmt.Table.TableAlias);
    }

    [Fact]
    public void Select_BacktickQuoted_WithJoin()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT `c`.`CustomerName`, `o`.`OrderDate` FROM `Customers` `c` INNER JOIN `Orders` `o` ON `c`.`ID` = `o`.`CustomerID`");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.Equal(2, selectStmt.Columns.Count);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("CustomerName", firstColumn.ColumnName);

        var secondColumn = selectStmt.Columns[1] as SqlColumn;
        Assert.Equal("OrderDate", secondColumn.ColumnName);

        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.Single(selectStmt.Joins);
        Assert.Equal("Orders", selectStmt.Joins[0].Table.TableName);
    }

    [Fact]
    public void Select_BacktickQuoted_WithWhere()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT `ID` FROM `Customers` WHERE `CustomerName` = 'Alice'");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.NotNull(selectStmt.WhereClause);
    }

    [Fact]
    public void Select_GroupBy_WithRollup_SingleColumn()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT department, SUM(salary) FROM employees GROUP BY department WITH ROLLUP");

        var selectStmt = grammar.Create(node);

        // WITH ROLLUP converts all columns into a single ROLLUP grouping set
        Assert.NotNull(selectStmt.GroupBy);
        Assert.Empty(selectStmt.GroupBy.Columns);
        Assert.Single(selectStmt.GroupBy.GroupingSets);

        var rollupSet = selectStmt.GroupBy.GroupingSets[0];
        Assert.Equal(GroupingSetType.Rollup, rollupSet.Type);
        Assert.Single(rollupSet.Sets);
        Assert.Equal("department", rollupSet.Sets[0][0]);
    }

    [Fact]
    public void Select_GroupBy_WithRollup_MultipleColumns()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT year, quarter, SUM(sales) FROM revenue GROUP BY year, quarter WITH ROLLUP");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        Assert.Empty(selectStmt.GroupBy.Columns);
        Assert.Single(selectStmt.GroupBy.GroupingSets);

        var rollupSet = selectStmt.GroupBy.GroupingSets[0];
        Assert.Equal(GroupingSetType.Rollup, rollupSet.Type);
        Assert.Equal(2, rollupSet.Sets.Count);
        Assert.Equal("year", rollupSet.Sets[0][0]);
        Assert.Equal("quarter", rollupSet.Sets[1][0]);
    }

    [Fact]
    public void Select_GroupBy_WithRollup_AndLimit()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT department, COUNT(*) FROM employees GROUP BY department WITH ROLLUP LIMIT 10");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        Assert.Single(selectStmt.GroupBy.GroupingSets);
        Assert.Equal(GroupingSetType.Rollup, selectStmt.GroupBy.GroupingSets[0].Type);

        Assert.NotNull(selectStmt.Limit);
        Assert.Equal(10, selectStmt.Limit.RowCount.Value);
    }

    [Fact]
    public void Select_GroupBy_WithoutRollup_StillWorks()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT department, COUNT(*) FROM employees GROUP BY department");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        Assert.Single(selectStmt.GroupBy.Columns);
        Assert.Equal("department", selectStmt.GroupBy.Columns[0]);
        Assert.Empty(selectStmt.GroupBy.GroupingSets);
    }

    [Fact]
    public void Select_GroupBy_WithRollup_BacktickQuoted()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT `dept`, SUM(`salary`) FROM `employees` GROUP BY `dept` WITH ROLLUP");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        Assert.Single(selectStmt.GroupBy.GroupingSets);

        var rollupSet = selectStmt.GroupBy.GroupingSets[0];
        Assert.Equal(GroupingSetType.Rollup, rollupSet.Type);
        Assert.Single(rollupSet.Sets);
        Assert.Equal("dept", rollupSet.Sets[0][0]);
    }

    [Fact]
    public void Select_GroupBy_StandardRollup_StillWorks()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT year, SUM(sales) FROM revenue GROUP BY ROLLUP(year)");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        Assert.Empty(selectStmt.GroupBy.Columns);
        Assert.Single(selectStmt.GroupBy.GroupingSets);

        var rollupSet = selectStmt.GroupBy.GroupingSets[0];
        Assert.Equal(GroupingSetType.Rollup, rollupSet.Type);
        Assert.Single(rollupSet.Sets);
        Assert.Equal("year", rollupSet.Sets[0][0]);
    }

    [Fact]
    public void Select_WithSimpleLimit_Parameter()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT b.BlogId, b.Url FROM Blogs AS b LIMIT @__p_0");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert on select columns
        Assert.Equal(2, selectStmt.Columns.Count);

        Assert.Empty(selectStmt.Joins);

        //WHERE
        Assert.Null(selectStmt.WhereClause);

        //LIMIT
        Assert.NotNull(selectStmt.Limit);
        Assert.NotNull(selectStmt.Limit.RowCount.Parameter);
        Assert.Equal("__p_0", selectStmt.Limit.RowCount.Parameter.Name);
        Assert.Null(selectStmt.Limit.RowOffset.Parameter);
    }

    // ── MySQL-specific function tests ──────────────────────────────────────

    [Fact]
    public void Select_NowFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT NOW()");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("NOW", funcColumn.Function.FunctionName);
        Assert.Empty(funcColumn.Function.Arguments);
    }

    [Fact]
    public void Select_CurdateFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT CURDATE()");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("CURDATE", funcColumn.Function.FunctionName);
        Assert.Empty(funcColumn.Function.Arguments);
    }

    [Fact]
    public void Select_CoalesceFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT COALESCE(name, email, 'unknown') FROM users");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("COALESCE", funcColumn.Function.FunctionName);
        Assert.Equal(3, funcColumn.Function.Arguments.Count);
    }

    [Fact]
    public void Select_IfnullFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT IFNULL(nickname, 'N/A') FROM users");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("IFNULL", funcColumn.Function.FunctionName);
        Assert.Equal(2, funcColumn.Function.Arguments.Count);
    }

    [Fact]
    public void Select_IfFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT IF(status = 'active', 1, 0) FROM users");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("IF", funcColumn.Function.FunctionName);
        Assert.Equal(3, funcColumn.Function.Arguments.Count);
    }

    [Fact]
    public void Select_ConcatWsFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT CONCAT_WS(', ', city, state, country) FROM addresses");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("CONCAT_WS", funcColumn.Function.FunctionName);
        Assert.Equal(4, funcColumn.Function.Arguments.Count);
    }

    [Fact]
    public void Select_DateFormatFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DATE_FORMAT(created_at, '%Y-%m-%d') FROM orders");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("DATE_FORMAT", funcColumn.Function.FunctionName);
        Assert.Equal(2, funcColumn.Function.Arguments.Count);
    }

    [Fact]
    public void Select_StrToDateFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT STR_TO_DATE('21/03/2025', '%d/%m/%Y')");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("STR_TO_DATE", funcColumn.Function.FunctionName);
        Assert.Equal(2, funcColumn.Function.Arguments.Count);
    }

    [Fact]
    public void Select_DatediffFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DATEDIFF(end_date, start_date) FROM projects");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("DATEDIFF", funcColumn.Function.FunctionName);
        Assert.Equal(2, funcColumn.Function.Arguments.Count);
    }

    [Fact]
    public void Select_DateAddFunction_WithInterval()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DATE_ADD(created_at, INTERVAL 30 DAY) FROM orders");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("DATE_ADD", funcColumn.Function.FunctionName);
        Assert.Equal(2, funcColumn.Function.Arguments.Count);

        // Second argument is an INTERVAL function node
        var intervalArg = funcColumn.Function.Arguments[1];
        Assert.NotNull(intervalArg.Function);
        Assert.Equal("INTERVAL", intervalArg.Function.FunctionName);
        Assert.Equal(2, intervalArg.Function.Arguments.Count);
        // Unit is stored as a string literal
        Assert.Equal("DAY", intervalArg.Function.Arguments[1].Value?.Value?.ToString());
    }

    [Fact]
    public void Select_DateSubFunction_WithInterval()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DATE_SUB(NOW(), INTERVAL 7 DAY) FROM dual");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("DATE_SUB", funcColumn.Function.FunctionName);
        Assert.Equal(2, funcColumn.Function.Arguments.Count);

        var intervalArg = funcColumn.Function.Arguments[1];
        Assert.NotNull(intervalArg.Function);
        Assert.Equal("INTERVAL", intervalArg.Function.FunctionName);
    }

    [Fact]
    public void Select_DateAddFunction_WithMonthInterval()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DATE_ADD('2025-01-15', INTERVAL 3 MONTH)");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("DATE_ADD", funcColumn.Function.FunctionName);

        var intervalArg = funcColumn.Function.Arguments[1];
        Assert.NotNull(intervalArg.Function);
        Assert.Equal("MONTH", intervalArg.Function.Arguments[1].Value?.Value?.ToString());
    }

    [Fact]
    public void Select_DateSubFunction_WithYearInterval()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DATE_SUB(hire_date, INTERVAL 1 YEAR) FROM employees");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("DATE_SUB", funcColumn.Function.FunctionName);

        var intervalArg = funcColumn.Function.Arguments[1];
        Assert.NotNull(intervalArg.Function);
        Assert.Equal("YEAR", intervalArg.Function.Arguments[1].Value?.Value?.ToString());
    }

    [Fact]
    public void Select_DateAddFunction_WithHourInterval()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DATE_ADD(timestamp_col, INTERVAL 2 HOUR) FROM events");

        var selectStmt = grammar.Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("DATE_ADD", funcColumn.Function.FunctionName);

        var intervalArg = funcColumn.Function.Arguments[1];
        Assert.Equal("HOUR", intervalArg.Function.Arguments[1].Value?.Value?.ToString());
    }

    [Fact]
    public void Select_IfFunction_WithAlias()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT IF(score >= 60, 'pass', 'fail') AS result FROM exams");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("IF", funcColumn.Function.FunctionName);
        Assert.Equal("result", funcColumn.ColumnAlias);
    }

    [Fact]
    public void Select_NowFunction_InWhere()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT id FROM events WHERE event_date > NOW()");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.WhereClause);
        // The WHERE clause contains a binary expression with NOW() on the right
        Assert.NotNull(selectStmt.WhereClause.BinExpr);
    }

    [Fact]
    public void Select_CoalesceFunction_WithBackticks()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT COALESCE(`nickname`, `name`, 'Anonymous') FROM `users`");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("COALESCE", funcColumn.Function.FunctionName);
        Assert.Equal(3, funcColumn.Function.Arguments.Count);
    }

    [Fact]
    public void Select_NestedFunctions()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DATE_FORMAT(NOW(), '%Y-%m-%d') AS today");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("DATE_FORMAT", funcColumn.Function.FunctionName);
        Assert.Equal(2, funcColumn.Function.Arguments.Count);

        // First argument is NOW() function
        var nowArg = funcColumn.Function.Arguments[0];
        Assert.NotNull(nowArg.Function);
        Assert.Equal("NOW", nowArg.Function.FunctionName);
    }

    [Fact]
    public void Select_MultipleMySqlFunctions()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT IFNULL(name, 'Unknown'), COALESCE(email, phone, 'none'), NOW() FROM users");

        var selectStmt = grammar.Create(node);

        Assert.Equal(3, selectStmt.Columns.Count);

        var col1 = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("IFNULL", col1.Function.FunctionName);

        var col2 = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[1]);
        Assert.Equal("COALESCE", col2.Function.FunctionName);

        var col3 = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal("NOW", col3.Function.FunctionName);
    }

    [Fact]
    public void Select_DateAddFunction_WithMinuteInterval()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DATE_ADD(login_time, INTERVAL 30 MINUTE) FROM sessions");

        var selectStmt = grammar.Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("DATE_ADD", funcColumn.Function.FunctionName);

        var intervalArg = funcColumn.Function.Arguments[1];
        Assert.Equal("MINUTE", intervalArg.Function.Arguments[1].Value?.Value?.ToString());
    }

    [Fact]
    public void Select_DateSubFunction_WithWeekInterval()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DATE_SUB(CURDATE(), INTERVAL 2 WEEK)");

        var selectStmt = grammar.Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("DATE_SUB", funcColumn.Function.FunctionName);

        // First arg is CURDATE() function
        Assert.NotNull(funcColumn.Function.Arguments[0].Function);
        Assert.Equal("CURDATE", funcColumn.Function.Arguments[0].Function.FunctionName);

        var intervalArg = funcColumn.Function.Arguments[1];
        Assert.Equal("WEEK", intervalArg.Function.Arguments[1].Value?.Value?.ToString());
    }

    [Fact]
    public void Select_IfFunction_WithNullComparison()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT IF(deleted_at IS NULL, 'active', 'deleted') FROM accounts");

        var selectStmt = grammar.Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("IF", funcColumn.Function.FunctionName);
        Assert.Equal(3, funcColumn.Function.Arguments.Count);
    }

    [Fact]
    public void Select_ConcatWsFunction_WithBacktickColumns()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT CONCAT_WS(' ', `first_name`, `last_name`) AS full_name FROM `employees`");

        var selectStmt = grammar.Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("CONCAT_WS", funcColumn.Function.FunctionName);
        Assert.Equal(3, funcColumn.Function.Arguments.Count);
        Assert.Equal("full_name", funcColumn.ColumnAlias);
    }

    [Fact]
    public void Select_DateFormatFunction_WithAlias()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DATE_FORMAT(order_date, '%M %d, %Y') AS formatted_date FROM orders");

        var selectStmt = grammar.Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("DATE_FORMAT", funcColumn.Function.FunctionName);
        Assert.Equal("formatted_date", funcColumn.ColumnAlias);
    }

    [Fact]
    public void Select_DatediffFunction_InWhere()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT id FROM subscriptions WHERE DATEDIFF(end_date, start_date) > 30");

        var selectStmt = grammar.Create(node);

        Assert.NotNull(selectStmt.WhereClause);
        Assert.NotNull(selectStmt.WhereClause.BinExpr);
    }

    [Fact]
    public void Select_DateAddFunction_WithSecondInterval()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DATE_ADD(ts, INTERVAL 90 SECOND) FROM logs");

        var selectStmt = grammar.Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        var intervalArg = funcColumn.Function.Arguments[1];
        Assert.Equal("SECOND", intervalArg.Function.Arguments[1].Value?.Value?.ToString());
    }

    [Fact]
    public void Select_DateAddFunction_WithQuarterInterval()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT DATE_ADD(report_date, INTERVAL 1 QUARTER) FROM reports");

        var selectStmt = grammar.Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        var intervalArg = funcColumn.Function.Arguments[1];
        Assert.Equal("QUARTER", intervalArg.Function.Arguments[1].Value?.Value?.ToString());
    }

}