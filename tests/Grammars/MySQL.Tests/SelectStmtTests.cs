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
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            MySQL.SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

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

}