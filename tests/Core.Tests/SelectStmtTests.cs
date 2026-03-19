using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class SelectStmtTests
{
    internal class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            ExprList exprList = new(this, expr);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = selectStmt;
        }

        public virtual SqlSelectDefinition Create(ParseTreeNode selectStmt, IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider) =>
            ((SelectStmt)Root).Create(selectStmt, databaseConnectionProvider, tableSchemaProvider);
    }

    [Fact]
    public void Select_NoJoin()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT ID, CustomerName FROM Customers");

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
        Assert.Null(selectStmt.Limit);
    }

    [Fact]
    public void Select_TableNotFound()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT ID, CustomerName FROM Cuuuustomers");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();

        //Assert

        //Note:  KeyNotFoundException is thrown by the TableSchemaProvider.GetColumns() method and therefore the type of 
        //       exception depends on the caller, not the SqlBuildingBlocks library.
        Assert.Throws<KeyNotFoundException>(() => grammar.Create(node, databaseConnectionProvider, tableSchemaProvider));
    }

    [Fact]
    public void Select_TableSchemaProvider_GetColumns_ReturnsNull()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT ID, CustomerName FROM Customer JOIN Sales ON Customer.ID = Sales.CustomerID");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        MeanTableSchemaProvider tableSchemaProvider = new();

        //Act
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert
        Assert.True(selectStmt.InvalidReferences);
    }


    [Fact]
    public void Select_WithJoinsAndTableAliases()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT [c].[CustomerName], [o].[OrderDate], [oi].[Quantity], [p].[Name] 
            FROM [Customers] [c] INNER JOIN [Orders] [o] ON [c].[ID] = [o].[CustomerID] 
                INNER JOIN [OrderItems] [oi] ON [o].[ID] = [oi].[OrderID] 
                INNER JOIN [Products] AS p ON [p].[ID] = [oi].[ProductID]");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert on select columns
        Assert.Equal(4, selectStmt.Columns.Count);

        //First column - [c].[CustomerName]
        Assert.IsType<SqlColumn>(selectStmt.Columns[0]);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("CustomerName", firstColumn.ColumnName);
        Assert.Equal("c", firstColumn.TableName);
        Assert.NotNull(firstColumn.TableRef);
        Assert.Equal("Customers", firstColumn.TableRef.TableName);

        //Second column - [o].[OrderDate]
        Assert.IsType<SqlColumn>(selectStmt.Columns[1]);
        var secondColumn = selectStmt.Columns[1] as SqlColumn;
        Assert.Equal("OrderDate", secondColumn.ColumnName);
        Assert.Equal("o", secondColumn.TableName);
        Assert.NotNull(secondColumn.TableRef);
        Assert.Equal("Orders", secondColumn.TableRef.TableName);

        //Third column - [oi].[Quantity]
        Assert.IsType<SqlColumn>(selectStmt.Columns[2]);
        var thirdColumn = selectStmt.Columns[2] as SqlColumn;
        Assert.Equal("Quantity", thirdColumn.ColumnName);
        Assert.Equal("oi", thirdColumn.TableName);
        Assert.NotNull(thirdColumn.TableRef);
        Assert.Equal("OrderItems", thirdColumn.TableRef.TableName);

        //Fourth column - [p].[Name]
        Assert.IsType<SqlColumn>(selectStmt.Columns[3]);
        var fourthColumn = selectStmt.Columns[3] as SqlColumn;
        Assert.Equal("Name", fourthColumn.ColumnName);
        Assert.Equal("p", fourthColumn.TableName);
        Assert.NotNull(fourthColumn.TableRef);
        Assert.Equal("Products", fourthColumn.TableRef.TableName);

        //Assert on FROM table.
        Assert.Equal("MyDatabase", selectStmt.Table.DatabaseName);
        Assert.Equal("Customers", selectStmt.Table.TableName);

        //Many assertions are not done on the joins, because JoinChainOptTests.MultipleInnerJoins_WithColumnId_Expressions already thoroughly covers them.
        Assert.Equal(3, selectStmt.Joins.Count);

        //WHERE
        Assert.Null(selectStmt.WhereClause);

        //LIMIT
        Assert.Null(selectStmt.Limit);
    }

    [Fact]
    public void Select_AllTableColumns()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT p.* 
            FROM [Customers] [c] INNER JOIN [Orders] [o] ON [c].[ID] = [o].[CustomerID] 
                INNER JOIN [OrderItems] [oi] ON [o].[ID] = [oi].[OrderID] 
                INNER JOIN [Products] AS p ON [p].[ID] = [oi].[ProductID]");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert on select columns
        Assert.Single(selectStmt.Columns);

        //First column - p.*
        Assert.IsType<SqlAllColumns>(selectStmt.Columns[0]);
        var firstColumn = selectStmt.Columns[0] as SqlAllColumns;
        Assert.Equal("p", firstColumn.TableName);
        Assert.Single(firstColumn.TableRefs);
        Assert.Equal("Products", firstColumn.TableRefs[0].TableName);

        //Assert on FROM table.
        Assert.Equal("MyDatabase", selectStmt.Table.DatabaseName);
        Assert.Equal("Customers", selectStmt.Table.TableName);

        //Many assertions are not done on the joins, because JoinChainOptTests.MultipleInnerJoins_WithColumnId_Expressions already thoroughly covers them.
        Assert.Equal(3, selectStmt.Joins.Count);

        //WHERE
        Assert.Null(selectStmt.WhereClause);

        //LIMIT
        Assert.Null(selectStmt.Limit);
    }

    [Fact]
    public void Select_WithFunctionCall()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT BlogId FROM Blogs WHERE ROW_COUNT() = 1 AND BlogId=LAST_INSERT_ID()");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert on select columns
        Assert.Single(selectStmt.Columns);

        //First column - [c].[CustomerName]
        Assert.IsType<SqlColumn>(selectStmt.Columns[0]);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("BlogId", firstColumn.ColumnName);
        Assert.NotNull(firstColumn.TableRef);
        Assert.Equal("Blogs", firstColumn.TableRef.TableName);

        //WHERE
        Assert.NotNull(selectStmt.WhereClause);

        //WHERE - ROW_COUNT() = 1
        var whereLeftExpr = selectStmt.WhereClause!.BinExpr!.Left.BinExpr;
        Assert.NotNull(whereLeftExpr);
        Assert.NotNull(whereLeftExpr.Left.Function);
        Assert.Equal("ROW_COUNT", whereLeftExpr.Left.Function.FunctionName);
        Assert.Empty(whereLeftExpr.Left.Function.Arguments);
        Assert.NotNull(whereLeftExpr.Right.Value);
        Assert.Equal(1, whereLeftExpr.Right.Value.Int);

        //WHERE - BlogId=LAST_INSERT_ID()
        var whereRightExpr = selectStmt.WhereClause!.BinExpr!.Right.BinExpr;
        Assert.NotNull(whereRightExpr);
        Assert.NotNull(whereRightExpr.Left.Column);
        Assert.Equal("BlogId", whereRightExpr.Left.Column.ColumnName);
        Assert.NotNull(whereRightExpr.Right.Function);
        Assert.Equal("LAST_INSERT_ID", whereRightExpr.Right.Function.FunctionName);
        Assert.Empty(whereRightExpr.Right.Function.Arguments);
    }

    [Fact]
    public void Select_WithFunctionColumn()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT LAST_INSERT_ID() AS Id");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.Single(selectStmt.Columns);
        var column = selectStmt.Columns[0];
        Assert.IsType<SqlFunctionColumn>(column);
        var functionColumn = (SqlFunctionColumn)column;
        Assert.Equal("LAST_INSERT_ID", functionColumn.Function.FunctionName);
    }

    [Fact]
    public void Select_WithCountStar()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT COUNT(*) FROM Blogs");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.Single(selectStmt.Columns);
        var column = selectStmt.Columns[0];
        Assert.IsType<SqlAggregate>(column);
        var aggregateColumn = (SqlAggregate)column;
        Assert.Equal("COUNT", aggregateColumn.AggregateName);
        Assert.Null(aggregateColumn.Argument);
    }

    [Fact]
    public void Select_AllColumns()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT * FROM OrderItems");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert: Columns of SELECT
        Assert.Single(selectStmt.Columns);
        var iColumn1 = selectStmt.Columns[0];
        Assert.Equal(typeof(SqlAllColumns), iColumn1.GetType());
        var column1 = (SqlAllColumns)iColumn1;
        Assert.True(string.IsNullOrEmpty(column1.TableName));

        //Assert: Tables of FROM
        Assert.NotNull(selectStmt.Table);
        var table = selectStmt.Table;
        Assert.Equal("OrderItems", table.TableName);
        Assert.True(string.IsNullOrEmpty(table.TableAlias));

    }

    [Fact]
    public void Select_ColumnAlias()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT ProductID PI FROM OrderItems");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert: Columns of SELECT
        Assert.Single(selectStmt.Columns);
        var iColumn = selectStmt.Columns[0];
        Assert.Equal(typeof(SqlColumn), iColumn.GetType());

        var column = (SqlColumn)iColumn;
        Assert.Equal("ProductID", column.ColumnName);
        Assert.True(string.IsNullOrEmpty(column.TableName));
        Assert.Equal("PI", column.ColumnAlias);

        //Assert: Tables of FROM
        Assert.NotNull(selectStmt.Table);
        var table = selectStmt.Table;
        Assert.Equal("OrderItems", table.TableName);

    }

    [Fact]
    public void Select_Where_IsNull()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT id, name FROM employees WHERE manager_id IS NULL");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert on FROM table
        Assert.Equal("employees", selectStmt.Table.TableName);

        //Assert on WHERE clause — WhereClause is a SqlExpression wrapping a SqlBinaryExpression
        Assert.NotNull(selectStmt.WhereClause);
        var binExpr = selectStmt.WhereClause!.BinExpr;
        Assert.NotNull(binExpr);
        Assert.Equal(SqlBinaryOperator.IsNull, binExpr!.Operator);
        Assert.NotNull(binExpr.Left.Column);
        Assert.Equal("manager_id", binExpr.Left.Column.ColumnName);
        Assert.Null(binExpr.Right);
    }

    [Fact]
    public void Select_Where_IsNotNull()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT id FROM orders WHERE shipped_date IS NOT NULL");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert on FROM table
        Assert.Equal("orders", selectStmt.Table.TableName);

        //Assert on WHERE clause — WhereClause is a SqlExpression wrapping a SqlBinaryExpression
        Assert.NotNull(selectStmt.WhereClause);
        var binExpr = selectStmt.WhereClause!.BinExpr;
        Assert.NotNull(binExpr);
        Assert.Equal(SqlBinaryOperator.IsNotNull, binExpr!.Operator);
        Assert.NotNull(binExpr.Left.Column);
        Assert.Equal("shipped_date", binExpr.Left.Column.ColumnName);
        Assert.Null(binExpr.Right);
    }

    [Fact]
    public void Select_Where_IsNull_And_IsNotNull()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT id FROM tasks WHERE completed_at IS NULL AND assigned_to IS NOT NULL");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert on WHERE: top-level AND
        Assert.NotNull(selectStmt.WhereClause);
        var andExpr = selectStmt.WhereClause!.BinExpr;
        Assert.NotNull(andExpr);
        Assert.Equal(SqlBinaryOperator.And, andExpr!.Operator);

        // Left side of AND: completed_at IS NULL
        var leftIsNull = andExpr.Left.BinExpr;
        Assert.NotNull(leftIsNull);
        Assert.Equal(SqlBinaryOperator.IsNull, leftIsNull.Operator);
        Assert.Equal("completed_at", leftIsNull.Left.Column.ColumnName);
        Assert.Null(leftIsNull.Right);

        // Right side of AND: assigned_to IS NOT NULL
        Assert.NotNull(andExpr.Right);
        var rightIsNotNull = andExpr.Right.BinExpr;
        Assert.NotNull(rightIsNotNull);
        Assert.Equal(SqlBinaryOperator.IsNotNull, rightIsNotNull.Operator);
        Assert.Equal("assigned_to", rightIsNotNull.Left.Column.ColumnName);
        Assert.Null(rightIsNotNull.Right);
    }

    [Fact]
    public void Select_Join_Where_IsNull()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT e.id, e.name FROM employees e INNER JOIN orders o ON e.id = o.id WHERE e.manager_id IS NULL");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert: one join
        Assert.Single(selectStmt.Joins);

        //Assert on WHERE clause: IS NULL — WhereClause is a SqlExpression wrapping a SqlBinaryExpression
        Assert.NotNull(selectStmt.WhereClause);
        var binExpr = selectStmt.WhereClause!.BinExpr;
        Assert.NotNull(binExpr);
        Assert.Equal(SqlBinaryOperator.IsNull, binExpr!.Operator);
        Assert.Null(binExpr.Right);
    }

    // ── BETWEEN / NOT BETWEEN ─────────────────────────────────────────────

    [Fact]
    public void Select_Where_Between()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT * FROM orders WHERE amount BETWEEN 100 AND 500");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.NotNull(selectStmt.WhereClause);
        var betweenExpr = selectStmt.WhereClause!.BetweenExpr;
        Assert.NotNull(betweenExpr);
        Assert.False(betweenExpr!.IsNegated);

        // Operand
        Assert.NotNull(betweenExpr.Operand.Column);
        Assert.Equal("amount", betweenExpr.Operand.Column.ColumnName);

        // Lower bound
        Assert.NotNull(betweenExpr.LowerBound.Value);
        Assert.Equal(100, betweenExpr.LowerBound.Value.Int);

        // Upper bound
        Assert.NotNull(betweenExpr.UpperBound.Value);
        Assert.Equal(500, betweenExpr.UpperBound.Value.Int);
    }

    [Fact]
    public void Select_Where_NotBetween()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT * FROM employees WHERE age NOT BETWEEN 18 AND 25");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.NotNull(selectStmt.WhereClause);
        var betweenExpr = selectStmt.WhereClause!.BetweenExpr;
        Assert.NotNull(betweenExpr);
        Assert.True(betweenExpr!.IsNegated);

        Assert.NotNull(betweenExpr.Operand.Column);
        Assert.Equal("age", betweenExpr.Operand.Column.ColumnName);

        Assert.NotNull(betweenExpr.LowerBound.Value);
        Assert.Equal(18, betweenExpr.LowerBound.Value.Int);

        Assert.NotNull(betweenExpr.UpperBound.Value);
        Assert.Equal(25, betweenExpr.UpperBound.Value.Int);
    }

    [Fact]
    public void Select_Where_Between_AndCondition()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT * FROM orders WHERE amount BETWEEN 10 AND 100 AND status = 'active'");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.NotNull(selectStmt.WhereClause);

        // Top-level is a logical AND
        var topBinExpr = selectStmt.WhereClause!.BinExpr;
        Assert.NotNull(topBinExpr);
        Assert.Equal(SqlBinaryOperator.And, topBinExpr!.Operator);

        // Left side of AND is the BETWEEN expression
        var betweenExpr = topBinExpr.Left.BetweenExpr;
        Assert.NotNull(betweenExpr);
        Assert.False(betweenExpr!.IsNegated);
        Assert.Equal("amount", betweenExpr.Operand.Column.ColumnName);
        Assert.Equal(10, betweenExpr.LowerBound.Value.Int);
        Assert.Equal(100, betweenExpr.UpperBound.Value.Int);

        // Right side of AND is the equality
        var rightBinExpr = topBinExpr.Right!.BinExpr;
        Assert.NotNull(rightBinExpr);
        Assert.Equal(SqlBinaryOperator.Equal, rightBinExpr!.Operator);
        Assert.Equal("status", rightBinExpr.Left.Column.ColumnName);
    }

    [Fact]
    public void Select_Union_ChainsSetOperation()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT id FROM employees UNION SELECT id FROM contractors");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Single(selectStmt.SetOperations);
        Assert.Equal(SqlSetOperator.Union, selectStmt.SetOperations[0].Operator);
        Assert.Equal("employees", selectStmt.Table!.TableName);
        Assert.Equal("contractors", selectStmt.SetOperations[0].Right.Table!.TableName);
    }

    [Fact]
    public void Select_UnionAll_Intersect_Except_AreParsedInOrder()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT id FROM a UNION ALL SELECT id FROM b INTERSECT SELECT id FROM c EXCEPT SELECT id FROM d");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Equal(3, selectStmt.SetOperations.Count);
        Assert.Equal(SqlSetOperator.UnionAll, selectStmt.SetOperations[0].Operator);
        Assert.Equal("b", selectStmt.SetOperations[0].Right.Table!.TableName);
        Assert.Equal(SqlSetOperator.Intersect, selectStmt.SetOperations[1].Operator);
        Assert.Equal("c", selectStmt.SetOperations[1].Right.Table!.TableName);
        Assert.Equal(SqlSetOperator.Except, selectStmt.SetOperations[2].Operator);
        Assert.Equal("d", selectStmt.SetOperations[2].Right.Table!.TableName);
    }

    [Fact]
    public void Select_Union_WithOrderBy_AppliesToFinalResult()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT id FROM employees UNION SELECT id FROM contractors ORDER BY id DESC");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Single(selectStmt.SetOperations);
        Assert.Single(selectStmt.OrderBy);
        Assert.Equal("id", selectStmt.OrderBy[0].ColumnName);
        Assert.True(selectStmt.OrderBy[0].Descending);
        Assert.Empty(selectStmt.SetOperations[0].Right.OrderBy);
    }

    [Fact]
    public void Select_WithDerivedTable_ResolvesOuterReferences()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT [customer_id], [total] FROM (SELECT [customer_id], SUM([amount]) AS [total] FROM [orders] GROUP BY [customer_id]) AS dt WHERE [total] > 1000");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.False(selectStmt.InvalidReferences, selectStmt.InvalidReferenceReason);
        Assert.IsType<SqlDerivedTable>(selectStmt.Table);

        var derivedTable = (SqlDerivedTable)selectStmt.Table!;
        Assert.Equal("dt", derivedTable.TableAlias);
        Assert.Equal(2, derivedTable.SelectDefinition.Columns.Count);

        var customerId = Assert.IsType<SqlColumn>(selectStmt.Columns[0]);
        Assert.Equal("customer_id", customerId.ColumnName);
        Assert.Same(derivedTable, customerId.TableRef);

        var total = Assert.IsType<SqlColumn>(selectStmt.Columns[1]);
        Assert.Equal("total", total.ColumnName);
        Assert.Same(derivedTable, total.TableRef);

        var whereColumn = Assert.IsType<SqlColumn>(selectStmt.WhereClause?.BinExpr?.Left.Column?.Column);
        Assert.Same(derivedTable, whereColumn.TableRef);
    }

    [Fact]
    public void Select_DerivedTableAllColumns_ExpandsInnerProjection()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM (SELECT [customer_id], SUM([amount]) AS [total] FROM [orders] GROUP BY [customer_id]) AS dt");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.False(selectStmt.InvalidReferences, selectStmt.InvalidReferenceReason);

        var allColumns = Assert.IsType<SqlAllColumns>(selectStmt.Columns[0]);
        Assert.NotNull(allColumns.Columns);
        Assert.Equal(2, allColumns.Columns!.Count);
        Assert.Equal("customer_id", allColumns.Columns[0].ColumnName);
        Assert.Equal("total", allColumns.Columns[1].ColumnName);
    }

    [Fact]
    public void Select_WithJoinAgainstDerivedTable_ResolvesJoinColumns()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT [name], [total] FROM [employees] AS emp INNER JOIN (SELECT [customer_id], SUM([amount]) AS [total] FROM [orders] GROUP BY [customer_id]) AS dt ON [id] = [customer_id] WHERE [total] > 1000");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.False(selectStmt.InvalidReferences, selectStmt.InvalidReferenceReason);
        Assert.Single(selectStmt.Joins);
        Assert.IsType<SqlDerivedTable>(selectStmt.Joins[0].Table);

        var derivedJoin = (SqlDerivedTable)selectStmt.Joins[0].Table;
        Assert.Equal("dt", derivedJoin.TableAlias);

        var joinRightColumn = Assert.IsType<SqlColumn>(selectStmt.Joins[0].Condition.Right.Column!.Column);
        Assert.Same(derivedJoin, joinRightColumn.TableRef);

        var total = Assert.IsType<SqlColumn>(selectStmt.Columns[1]);
        Assert.Same(derivedJoin, total.TableRef);
    }

    [Fact]
    public void Select_WithExists_ResolvesNonCorrelatedSubquery()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM [employees] WHERE EXISTS (SELECT [id] FROM [orders] WHERE [amount] > 100)");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.False(selectStmt.InvalidReferences, selectStmt.InvalidReferenceReason);
        Assert.NotNull(selectStmt.WhereClause?.ExistsExpr);

        var existsExpr = selectStmt.WhereClause!.ExistsExpr!;
        Assert.False(existsExpr.IsNegated);
        Assert.Equal("orders", existsExpr.SelectDefinition.Table!.TableName);
        Assert.False(existsExpr.SelectDefinition.InvalidReferences, existsExpr.SelectDefinition.InvalidReferenceReason);
    }

    [Fact]
    public void Select_WithExists_ResolvesCorrelatedOuterReference()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM [employees] WHERE EXISTS (SELECT [id] FROM [orders] WHERE [orders].[customer_id] = [employees].[id])");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.False(selectStmt.InvalidReferences, selectStmt.InvalidReferenceReason);

        var existsExpr = selectStmt.WhereClause!.ExistsExpr!;
        var innerCondition = existsExpr.SelectDefinition.WhereClause!.BinExpr!;

        var leftColumn = Assert.IsType<SqlColumn>(innerCondition.Left.Column!.Column);
        Assert.Equal("orders", leftColumn.TableRef!.TableName);

        var rightColumn = Assert.IsType<SqlColumn>(innerCondition.Right!.Column!.Column);
        Assert.Equal("employees", rightColumn.TableRef!.TableName);
        Assert.Same(selectStmt.Table, rightColumn.TableRef);
    }

    [Fact]
    public void Select_WithNotExists_ResolvesSubquery()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM [employees] WHERE NOT EXISTS (SELECT [id] FROM [orders] WHERE [amount] > 100)");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.False(selectStmt.InvalidReferences, selectStmt.InvalidReferenceReason);
        Assert.True(selectStmt.WhereClause!.ExistsExpr!.IsNegated);
    }

    [Fact]
    public void Select_WithExistsContainingInSubquery_Parses()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM [employees] WHERE EXISTS (SELECT [id] FROM [orders] WHERE [customer_id] IN (SELECT [id] FROM [customers]))");

        Assert.Equal(SelectStmt.TermName, node.Term.Name);
    }

    [Fact]
    public void Select_WithDerivedTableContainingScalarSubquery_Parses()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM (SELECT [customer_id] FROM [orders] WHERE [amount] = (SELECT MAX([amount]) FROM [orders])) AS dt");

        Assert.Equal(SelectStmt.TermName, node.Term.Name);
    }

    [Fact]
    public void Select_WithScalarSubqueryInWhere_ResolvesCorrelatedOuterReference()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT * FROM [employees] WHERE [age] > (SELECT [amount] FROM [orders] WHERE [orders].[customer_id] = [employees].[id])");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.False(selectStmt.InvalidReferences, selectStmt.InvalidReferenceReason);

        var scalarSubquery = selectStmt.WhereClause!.BinExpr!.Right!.ScalarSubqueryExpr!;
        var innerCondition = scalarSubquery.SelectDefinition.WhereClause!.BinExpr!;

        var leftColumn = Assert.IsType<SqlColumn>(innerCondition.Left.Column!.Column);
        Assert.Equal("orders", leftColumn.TableRef!.TableName);

        var rightColumn = Assert.IsType<SqlColumn>(innerCondition.Right!.Column!.Column);
        Assert.Equal("employees", rightColumn.TableRef!.TableName);
        Assert.Same(selectStmt.Table, rightColumn.TableRef);
    }

    [Fact]
    public void Select_WithScalarSubqueryInProjection_ResolvesAliasForDerivedTable()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT [dt].[max_amount] FROM (SELECT [employees].[id], (SELECT [amount] FROM [orders] WHERE [orders].[customer_id] = [employees].[id]) AS [max_amount] FROM [employees]) AS [dt]");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.False(selectStmt.InvalidReferences, selectStmt.InvalidReferenceReason);
        Assert.IsType<SqlDerivedTable>(selectStmt.Table);

        var derivedTable = (SqlDerivedTable)selectStmt.Table!;
        var projectedSubquery = Assert.IsType<SqlScalarSubqueryColumn>(derivedTable.SelectDefinition.Columns[1]);

        Assert.Equal("max_amount", projectedSubquery.ColumnAlias);
        Assert.NotNull(projectedSubquery.ColumnType);

        var projectedColumn = Assert.IsType<SqlColumn>(selectStmt.Columns[0]);
        Assert.Same(derivedTable, projectedColumn.TableRef);
    }

    // ── WITH clause (CTEs) ──────────────────────────────────────────────

    [Fact]
    public void Select_WithSingleCte_ParsesCteName()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "WITH active_employees AS (SELECT id, name FROM employees WHERE status = 'active') " +
            "SELECT id, name FROM active_employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Single(selectStmt.Ctes);
        Assert.Equal("active_employees", selectStmt.Ctes[0].Name);

        // CTE subquery
        var cteSelect = selectStmt.Ctes[0].SelectDefinition;
        Assert.Equal(2, cteSelect.Columns.Count);
        Assert.Equal("employees", cteSelect.Table!.TableName);
        Assert.NotNull(cteSelect.WhereClause);

        // Outer query
        Assert.Equal("active_employees", selectStmt.Table!.TableName);
        Assert.Equal(2, selectStmt.Columns.Count);
    }

    [Fact]
    public void Select_WithMultipleCtes_ParsesAll()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "WITH dept_totals AS (SELECT department_id, SUM(salary) AS total FROM employees GROUP BY department_id), " +
            "high_depts AS (SELECT department_id FROM dept_totals WHERE total > 100000) " +
            "SELECT name FROM employees WHERE department_id IN (SELECT department_id FROM high_depts)");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Equal(2, selectStmt.Ctes.Count);
        Assert.Equal("dept_totals", selectStmt.Ctes[0].Name);
        Assert.Equal("high_depts", selectStmt.Ctes[1].Name);

        // First CTE subquery
        var cte1 = selectStmt.Ctes[0].SelectDefinition;
        Assert.Equal("employees", cte1.Table!.TableName);

        // Second CTE references first CTE
        var cte2 = selectStmt.Ctes[1].SelectDefinition;
        Assert.Equal("dept_totals", cte2.Table!.TableName);

        // Outer query
        Assert.Equal("employees", selectStmt.Table!.TableName);
    }

    [Fact]
    public void Select_WithCteReferencingAnotherCte_Parses()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "WITH base AS (SELECT id, amount FROM orders), " +
            "filtered AS (SELECT id FROM base WHERE amount > 50), " +
            "final AS (SELECT id FROM filtered WHERE id > 10) " +
            "SELECT * FROM final");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Equal(3, selectStmt.Ctes.Count);
        Assert.Equal("base", selectStmt.Ctes[0].Name);
        Assert.Equal("filtered", selectStmt.Ctes[1].Name);
        Assert.Equal("final", selectStmt.Ctes[2].Name);

        // Verify chaining
        Assert.Equal("orders", selectStmt.Ctes[0].SelectDefinition.Table!.TableName);
        Assert.Equal("base", selectStmt.Ctes[1].SelectDefinition.Table!.TableName);
        Assert.Equal("filtered", selectStmt.Ctes[2].SelectDefinition.Table!.TableName);
        Assert.Equal("final", selectStmt.Table!.TableName);
    }

    [Fact]
    public void Select_WithoutCte_HasEmptyCtesList()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT id FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Empty(selectStmt.Ctes);
    }

    // ── WITH RECURSIVE clause ─────────────────────────────────────────────

    [Fact]
    public void Select_WithRecursiveCte_SetsIsRecursiveFlag()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "WITH RECURSIVE org_chart AS (" +
            "SELECT id, name, manager_id FROM employees WHERE manager_id IS NULL " +
            "UNION ALL " +
            "SELECT e.id, e.name, e.manager_id FROM employees e " +
            "JOIN org_chart o ON e.manager_id = o.id) " +
            "SELECT * FROM org_chart");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Single(selectStmt.Ctes);
        Assert.True(selectStmt.Ctes[0].IsRecursive);
        Assert.Equal("org_chart", selectStmt.Ctes[0].Name);

        // Anchor query
        var cteSelect = selectStmt.Ctes[0].SelectDefinition;
        Assert.Equal("employees", cteSelect.Table!.TableName);
        Assert.Equal(3, cteSelect.Columns.Count);

        // UNION ALL with recursive term
        Assert.Single(cteSelect.SetOperations);
        Assert.Equal(SqlSetOperator.UnionAll, cteSelect.SetOperations[0].Operator);

        // Outer query
        Assert.Equal("org_chart", selectStmt.Table!.TableName);
    }

    [Fact]
    public void Select_WithRecursiveCte_HierarchyTraversal()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "WITH RECURSIVE category_tree AS (" +
            "SELECT id, name, parent_id FROM categories WHERE parent_id IS NULL " +
            "UNION ALL " +
            "SELECT c.id, c.name, c.parent_id FROM categories c " +
            "JOIN category_tree ct ON c.parent_id = ct.id) " +
            "SELECT id, name FROM category_tree");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Single(selectStmt.Ctes);
        Assert.True(selectStmt.Ctes[0].IsRecursive);
        Assert.Equal("category_tree", selectStmt.Ctes[0].Name);

        // The CTE should have a UNION ALL set operation
        var cteSelect = selectStmt.Ctes[0].SelectDefinition;
        Assert.Single(cteSelect.SetOperations);
        Assert.Equal(SqlSetOperator.UnionAll, cteSelect.SetOperations[0].Operator);

        // Outer query
        Assert.Equal("category_tree", selectStmt.Table!.TableName);
        Assert.Equal(2, selectStmt.Columns.Count);
    }

    [Fact]
    public void Select_NonRecursiveCte_IsRecursiveIsFalse()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "WITH active_employees AS (SELECT id, name FROM employees WHERE status = 'active') " +
            "SELECT id, name FROM active_employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Single(selectStmt.Ctes);
        Assert.False(selectStmt.Ctes[0].IsRecursive);
    }

    [Fact]
    public void Select_WithRecursiveMultipleCtes_AllMarkedRecursive()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "WITH RECURSIVE ancestors AS (" +
            "SELECT id, parent_id FROM nodes WHERE id = 1 " +
            "UNION ALL " +
            "SELECT n.id, n.parent_id FROM nodes n JOIN ancestors a ON n.id = a.parent_id), " +
            "descendants AS (" +
            "SELECT id, parent_id FROM nodes WHERE id = 1 " +
            "UNION ALL " +
            "SELECT n.id, n.parent_id FROM nodes n JOIN descendants d ON n.parent_id = d.id) " +
            "SELECT * FROM ancestors");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Equal(2, selectStmt.Ctes.Count);
        Assert.True(selectStmt.Ctes[0].IsRecursive);
        Assert.True(selectStmt.Ctes[1].IsRecursive);
        Assert.Equal("ancestors", selectStmt.Ctes[0].Name);
        Assert.Equal("descendants", selectStmt.Ctes[1].Name);
    }

    // ── Window functions (OVER clause) ─────────────────────────────────────

    [Fact]
    public void Select_WindowFunction_PartitionBy()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT department_id, salary, " +
            "ROW_NUMBER() OVER (PARTITION BY department_id) AS row_num " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Equal(3, selectStmt.Columns.Count);

        // Third column: ROW_NUMBER() OVER (PARTITION BY department_id)
        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal("ROW_NUMBER", funcColumn.Function.FunctionName);
        Assert.Equal("row_num", funcColumn.ColumnAlias);
        Assert.True(funcColumn.Function.IsWindowFunction);

        var windowSpec = funcColumn.Function.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.Single(windowSpec!.PartitionBy);
        Assert.Empty(windowSpec.OrderBy);
        Assert.Null(windowSpec.Frame);
    }

    [Fact]
    public void Select_WindowFunction_OrderBy()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT name, salary, " +
            "RANK() OVER (ORDER BY salary DESC) AS salary_rank " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal("RANK", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);

        var windowSpec = funcColumn.Function.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.Empty(windowSpec!.PartitionBy);
        Assert.Single(windowSpec.OrderBy);
        Assert.Equal("salary", windowSpec.OrderBy[0].ColumnName);
        Assert.True(windowSpec.OrderBy[0].Descending);
    }

    [Fact]
    public void Select_WindowFunction_PartitionByAndOrderBy()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT department_id, name, salary, " +
            "ROW_NUMBER() OVER (PARTITION BY department_id ORDER BY salary DESC) AS rank " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[3]);
        Assert.Equal("ROW_NUMBER", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);

        var windowSpec = funcColumn.Function.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.Single(windowSpec!.PartitionBy);
        Assert.Single(windowSpec.OrderBy);
        Assert.Equal("salary", windowSpec.OrderBy[0].ColumnName);
        Assert.True(windowSpec.OrderBy[0].Descending);
    }

    [Fact]
    public void Select_WindowFunction_FrameClause_RowsBetween()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT id, value, " +
            "COUNT(*) OVER (ORDER BY id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS running_total " +
            "FROM transactions");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[2]);
        Assert.Equal("COUNT", aggColumn.AggregateName);
        Assert.True(aggColumn.IsWindowFunction);

        var windowSpec = aggColumn.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.Empty(windowSpec!.PartitionBy);
        Assert.Single(windowSpec.OrderBy);
        Assert.Equal("id", windowSpec.OrderBy[0].ColumnName);

        // Frame clause
        Assert.NotNull(windowSpec.Frame);
        Assert.Equal(WindowFrameMode.Rows, windowSpec.Frame!.Mode);
        Assert.Equal(WindowFrameBoundType.UnboundedPreceding, windowSpec.Frame.Start.Type);
        Assert.NotNull(windowSpec.Frame.End);
        Assert.Equal(WindowFrameBoundType.CurrentRow, windowSpec.Frame.End!.Type);
    }

    [Fact]
    public void Select_WindowFunction_FrameClause_RangeBetween()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT id, amount, " +
            "COUNT(*) OVER (ORDER BY id RANGE BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) AS overall_avg " +
            "FROM orders");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[2]);
        Assert.True(aggColumn.IsWindowFunction);

        var windowSpec = aggColumn.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.NotNull(windowSpec!.Frame);
        Assert.Equal(WindowFrameMode.Range, windowSpec.Frame!.Mode);
        Assert.Equal(WindowFrameBoundType.UnboundedPreceding, windowSpec.Frame.Start.Type);
        Assert.NotNull(windowSpec.Frame.End);
        Assert.Equal(WindowFrameBoundType.UnboundedFollowing, windowSpec.Frame.End!.Type);
    }

    [Fact]
    public void Select_WindowFunction_FrameClause_NumericOffset()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT id, value, " +
            "COUNT(*) OVER (ORDER BY id ROWS BETWEEN 3 PRECEDING AND 1 FOLLOWING) AS moving_avg " +
            "FROM measurements");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[2]);
        Assert.True(aggColumn.IsWindowFunction);

        var windowSpec = aggColumn.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.NotNull(windowSpec!.Frame);
        Assert.Equal(WindowFrameMode.Rows, windowSpec.Frame!.Mode);
        Assert.Equal(WindowFrameBoundType.Preceding, windowSpec.Frame.Start.Type);
        Assert.Equal(3, windowSpec.Frame.Start.Offset);
        Assert.NotNull(windowSpec.Frame.End);
        Assert.Equal(WindowFrameBoundType.Following, windowSpec.Frame.End!.Type);
        Assert.Equal(1, windowSpec.Frame.End.Offset);
    }

    [Fact]
    public void Select_WindowFunction_CombinedPartitionByOrderByFrame()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT department_id, employee_id, salary, " +
            "COUNT(*) OVER (PARTITION BY department_id ORDER BY employee_id " +
            "ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS dept_running_total " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[3]);
        Assert.Equal("COUNT", aggColumn.AggregateName);
        Assert.True(aggColumn.IsWindowFunction);

        var windowSpec = aggColumn.WindowSpecification;
        Assert.NotNull(windowSpec);

        // PARTITION BY
        Assert.Single(windowSpec!.PartitionBy);

        // ORDER BY
        Assert.Single(windowSpec.OrderBy);
        Assert.Equal("employee_id", windowSpec.OrderBy[0].ColumnName);
        Assert.False(windowSpec.OrderBy[0].Descending);

        // Frame
        Assert.NotNull(windowSpec.Frame);
        Assert.Equal(WindowFrameMode.Rows, windowSpec.Frame!.Mode);
        Assert.Equal(WindowFrameBoundType.UnboundedPreceding, windowSpec.Frame.Start.Type);
        Assert.Equal(WindowFrameBoundType.CurrentRow, windowSpec.Frame.End!.Type);
    }

    [Fact]
    public void Select_WindowFunction_EmptyOver()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT id, ROW_NUMBER() OVER () AS row_num FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[1]);
        Assert.Equal("ROW_NUMBER", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);

        var windowSpec = funcColumn.Function.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.Empty(windowSpec!.PartitionBy);
        Assert.Empty(windowSpec.OrderBy);
        Assert.Null(windowSpec.Frame);
    }

    [Fact]
    public void Select_WithoutOverClause_NoWindowSpecification()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT LAST_INSERT_ID() AS Id");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("LAST_INSERT_ID", funcColumn.Function.FunctionName);
        Assert.False(funcColumn.Function.IsWindowFunction);
        Assert.Null(funcColumn.Function.WindowSpecification);
    }

    [Fact]
    public void Select_AggregateWithoutOver_NoWindowSpecification()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT COUNT(*) FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[0]);
        Assert.Equal("COUNT", aggColumn.AggregateName);
        Assert.False(aggColumn.IsWindowFunction);
        Assert.Null(aggColumn.WindowSpecification);
    }

    [Fact]
    public void Select_WindowFunction_MultiplePartitionByColumns()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT department_id, region, salary, " +
            "DENSE_RANK() OVER (PARTITION BY department_id, region ORDER BY salary DESC) AS rank " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[3]);
        Assert.Equal("DENSE_RANK", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);

        var windowSpec = funcColumn.Function.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.Equal(2, windowSpec!.PartitionBy.Count);
        Assert.Single(windowSpec.OrderBy);
    }

    [Fact]
    public void Select_WindowFunction_MultipleOrderByColumns()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT name, " +
            "ROW_NUMBER() OVER (ORDER BY department_id ASC, salary DESC) AS row_num " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[1]);
        Assert.True(funcColumn.Function.IsWindowFunction);

        var windowSpec = funcColumn.Function.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.Equal(2, windowSpec!.OrderBy.Count);
        Assert.Equal("department_id", windowSpec.OrderBy[0].ColumnName);
        Assert.False(windowSpec.OrderBy[0].Descending);
        Assert.Equal("salary", windowSpec.OrderBy[1].ColumnName);
        Assert.True(windowSpec.OrderBy[1].Descending);
    }

    [Fact]
    public void Select_WindowFunction_FuncCallWithOverAndFrame()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT id, " +
            "SUM(value) OVER (PARTITION BY category ORDER BY id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS running " +
            "FROM transactions");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        // SUM(value) may be parsed as FuncCall or Aggregate depending on grammar resolution
        var column = selectStmt.Columns[1];
#nullable enable
        SqlWindowSpecification? windowSpec = null;
#nullable restore
        if (column is SqlFunctionColumn funcCol)
        {
            Assert.True(funcCol.Function.IsWindowFunction);
            windowSpec = funcCol.Function.WindowSpecification;
        }
        else if (column is SqlAggregate aggCol)
        {
            Assert.True(aggCol.IsWindowFunction);
            windowSpec = aggCol.WindowSpecification;
        }
        else
        {
            Assert.Fail("Expected SqlFunctionColumn or SqlAggregate");
        }

        Assert.NotNull(windowSpec);
        Assert.Single(windowSpec!.PartitionBy);
        Assert.Single(windowSpec.OrderBy);
        Assert.NotNull(windowSpec.Frame);
        Assert.Equal(WindowFrameMode.Rows, windowSpec.Frame!.Mode);
        Assert.Equal(WindowFrameBoundType.UnboundedPreceding, windowSpec.Frame.Start.Type);
        Assert.Equal(WindowFrameBoundType.CurrentRow, windowSpec.Frame.End!.Type);
    }

    [Fact]
    public void Select_WindowFunction_FrameWithoutBetween()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT id, value, " +
            "COUNT(*) OVER (ORDER BY id ROWS UNBOUNDED PRECEDING) AS running_total " +
            "FROM transactions");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[2]);
        Assert.True(aggColumn.IsWindowFunction);

        var windowSpec = aggColumn.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.NotNull(windowSpec!.Frame);
        Assert.Equal(WindowFrameMode.Rows, windowSpec.Frame!.Mode);
        Assert.Equal(WindowFrameBoundType.UnboundedPreceding, windowSpec.Frame.Start.Type);
        Assert.Null(windowSpec.Frame.End);
    }

    // ── Named window functions (#44) ────────────────────────────────────

    [Fact]
    public void Select_NamedWindowFunction_RowNumber()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, " +
            "ROW_NUMBER() OVER (ORDER BY hire_date) AS rn " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[1]);
        Assert.Equal("ROW_NUMBER", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);
        Assert.True(funcColumn.Function.IsNamedWindowFunction);
        Assert.Equal(WindowFunctionType.RowNumber, funcColumn.Function.WindowFunctionType);
        Assert.Empty(funcColumn.Function.Arguments);
        Assert.Equal("rn", funcColumn.ColumnAlias);

        var windowSpec = funcColumn.Function.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.Single(windowSpec!.OrderBy);
        Assert.Equal("hire_date", windowSpec.OrderBy[0].ColumnName);
    }

    [Fact]
    public void Select_NamedWindowFunction_Rank()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, salary, " +
            "RANK() OVER (ORDER BY salary DESC) AS salary_rank " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal("RANK", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);
        Assert.True(funcColumn.Function.IsNamedWindowFunction);
        Assert.Equal(WindowFunctionType.Rank, funcColumn.Function.WindowFunctionType);
        Assert.Empty(funcColumn.Function.Arguments);

        var windowSpec = funcColumn.Function.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.Single(windowSpec!.OrderBy);
        Assert.True(windowSpec.OrderBy[0].Descending);
    }

    [Fact]
    public void Select_NamedWindowFunction_DenseRank()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, salary, " +
            "DENSE_RANK() OVER (PARTITION BY department_id ORDER BY salary DESC) AS dense_rnk " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal("DENSE_RANK", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);
        Assert.True(funcColumn.Function.IsNamedWindowFunction);
        Assert.Equal(WindowFunctionType.DenseRank, funcColumn.Function.WindowFunctionType);
        Assert.Empty(funcColumn.Function.Arguments);

        var windowSpec = funcColumn.Function.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.Single(windowSpec!.PartitionBy);
        Assert.Single(windowSpec.OrderBy);
        Assert.True(windowSpec.OrderBy[0].Descending);
    }

    [Fact]
    public void Select_NamedWindowFunction_Ntile()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, salary, " +
            "NTILE(4) OVER (ORDER BY salary DESC) AS quartile " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal("NTILE", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);
        Assert.True(funcColumn.Function.IsNamedWindowFunction);
        Assert.Equal(WindowFunctionType.Ntile, funcColumn.Function.WindowFunctionType);
        Assert.Single(funcColumn.Function.Arguments);
        Assert.Equal("quartile", funcColumn.ColumnAlias);
    }

    [Fact]
    public void Select_NamedWindowFunction_Lag_SingleArg()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, salary, " +
            "LAG(salary) OVER (ORDER BY hire_date) AS prev_salary " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal("LAG", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);
        Assert.True(funcColumn.Function.IsNamedWindowFunction);
        Assert.Equal(WindowFunctionType.Lag, funcColumn.Function.WindowFunctionType);
        Assert.Single(funcColumn.Function.Arguments);
    }

    [Fact]
    public void Select_NamedWindowFunction_Lag_WithOffsetAndDefault()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, salary, " +
            "LAG(salary, 2, 0) OVER (ORDER BY hire_date) AS prev2_salary " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal("LAG", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);
        Assert.Equal(WindowFunctionType.Lag, funcColumn.Function.WindowFunctionType);
        Assert.Equal(3, funcColumn.Function.Arguments.Count);
    }

    [Fact]
    public void Select_NamedWindowFunction_Lead_SingleArg()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, salary, " +
            "LEAD(salary) OVER (ORDER BY hire_date) AS next_salary " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal("LEAD", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);
        Assert.True(funcColumn.Function.IsNamedWindowFunction);
        Assert.Equal(WindowFunctionType.Lead, funcColumn.Function.WindowFunctionType);
        Assert.Single(funcColumn.Function.Arguments);
    }

    [Fact]
    public void Select_NamedWindowFunction_Lead_WithOffsetAndDefault()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, salary, " +
            "LEAD(salary, 3, 0) OVER (PARTITION BY department_id ORDER BY hire_date) AS future_salary " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal("LEAD", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);
        Assert.Equal(WindowFunctionType.Lead, funcColumn.Function.WindowFunctionType);
        Assert.Equal(3, funcColumn.Function.Arguments.Count);

        var windowSpec = funcColumn.Function.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.Single(windowSpec!.PartitionBy);
        Assert.Single(windowSpec.OrderBy);
    }

    [Fact]
    public void Select_NamedWindowFunction_FirstValue()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, salary, " +
            "FIRST_VALUE(salary) OVER (PARTITION BY department_id ORDER BY salary DESC) AS highest_salary " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal("FIRST_VALUE", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);
        Assert.True(funcColumn.Function.IsNamedWindowFunction);
        Assert.Equal(WindowFunctionType.FirstValue, funcColumn.Function.WindowFunctionType);
        Assert.Single(funcColumn.Function.Arguments);
    }

    [Fact]
    public void Select_NamedWindowFunction_LastValue()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, salary, " +
            "LAST_VALUE(salary) OVER (PARTITION BY department_id ORDER BY salary " +
            "ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) AS lowest_salary " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal("LAST_VALUE", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);
        Assert.True(funcColumn.Function.IsNamedWindowFunction);
        Assert.Equal(WindowFunctionType.LastValue, funcColumn.Function.WindowFunctionType);
        Assert.Single(funcColumn.Function.Arguments);

        var windowSpec = funcColumn.Function.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.NotNull(windowSpec!.Frame);
        Assert.Equal(WindowFrameMode.Rows, windowSpec.Frame!.Mode);
    }

    [Fact]
    public void Select_NamedWindowFunction_NthValue()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, salary, " +
            "NTH_VALUE(salary, 3) OVER (PARTITION BY department_id ORDER BY salary DESC) AS third_highest " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal("NTH_VALUE", funcColumn.Function.FunctionName);
        Assert.True(funcColumn.Function.IsWindowFunction);
        Assert.True(funcColumn.Function.IsNamedWindowFunction);
        Assert.Equal(WindowFunctionType.NthValue, funcColumn.Function.WindowFunctionType);
        Assert.Equal(2, funcColumn.Function.Arguments.Count);
    }

    [Fact]
    public void Select_RegularFunction_IsNotNamedWindowFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT UPPER(name) FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[0]);
        Assert.Equal("UPPER", funcColumn.Function.FunctionName);
        Assert.False(funcColumn.Function.IsWindowFunction);
        Assert.False(funcColumn.Function.IsNamedWindowFunction);
        Assert.Equal(WindowFunctionType.None, funcColumn.Function.WindowFunctionType);
    }

    [Fact]
    public void Select_NamedWindowFunction_MultipleWindowFunctions()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, " +
            "ROW_NUMBER() OVER (ORDER BY salary) AS rn, " +
            "RANK() OVER (ORDER BY salary) AS rnk, " +
            "DENSE_RANK() OVER (ORDER BY salary) AS drnk " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Equal(4, selectStmt.Columns.Count);

        var rowNum = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[1]);
        Assert.Equal(WindowFunctionType.RowNumber, rowNum.Function.WindowFunctionType);

        var rank = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal(WindowFunctionType.Rank, rank.Function.WindowFunctionType);

        var denseRank = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[3]);
        Assert.Equal(WindowFunctionType.DenseRank, denseRank.Function.WindowFunctionType);
    }

    [Fact]
    public void Select_NamedWindowFunction_Lag_WithOffset()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, " +
            "LAG(salary, 1) OVER (ORDER BY hire_date) AS prev_salary " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[1]);
        Assert.Equal("LAG", funcColumn.Function.FunctionName);
        Assert.Equal(WindowFunctionType.Lag, funcColumn.Function.WindowFunctionType);
        Assert.Equal(2, funcColumn.Function.Arguments.Count);
    }

    [Fact]
    public void Select_NamedWindowFunction_Lead_WithOffset()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, " +
            "LEAD(salary, 1) OVER (ORDER BY hire_date) AS next_salary " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[1]);
        Assert.Equal("LEAD", funcColumn.Function.FunctionName);
        Assert.Equal(WindowFunctionType.Lead, funcColumn.Function.WindowFunctionType);
        Assert.Equal(2, funcColumn.Function.Arguments.Count);
    }

    [Fact]
    public void Select_NamedWindowFunction_FirstValue_WithFrame()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, " +
            "FIRST_VALUE(salary) OVER (ORDER BY salary ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS first_sal " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[1]);
        Assert.Equal("FIRST_VALUE", funcColumn.Function.FunctionName);
        Assert.Equal(WindowFunctionType.FirstValue, funcColumn.Function.WindowFunctionType);

        var windowSpec = funcColumn.Function.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.NotNull(windowSpec!.Frame);
        Assert.Equal(WindowFrameMode.Rows, windowSpec.Frame!.Mode);
        Assert.Equal(WindowFrameBoundType.UnboundedPreceding, windowSpec.Frame.Start.Type);
        Assert.Equal(WindowFrameBoundType.CurrentRow, windowSpec.Frame.End!.Type);
    }

    [Fact]
    public void Select_NamedWindowFunction_Ntile_WithPartitionBy()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT employee_id, department_id, " +
            "NTILE(3) OVER (PARTITION BY department_id ORDER BY salary DESC) AS tier " +
            "FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var funcColumn = Assert.IsType<SqlFunctionColumn>(selectStmt.Columns[2]);
        Assert.Equal("NTILE", funcColumn.Function.FunctionName);
        Assert.Equal(WindowFunctionType.Ntile, funcColumn.Function.WindowFunctionType);
        Assert.Single(funcColumn.Function.Arguments);

        var windowSpec = funcColumn.Function.WindowSpecification;
        Assert.NotNull(windowSpec);
        Assert.Single(windowSpec!.PartitionBy);
        Assert.Single(windowSpec.OrderBy);
        Assert.True(windowSpec.OrderBy[0].Descending);
    }

    // ── DISTINCT in aggregate functions (#45) ────────────────────────────

    [Fact]
    public void Select_CountDistinct()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT COUNT(DISTINCT category) FROM products");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Single(selectStmt.Columns);
        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[0]);
        Assert.Equal("COUNT", aggColumn.AggregateName);
        Assert.True(aggColumn.IsDistinct);
        Assert.NotNull(aggColumn.Argument);
    }

    [Fact]
    public void Select_SumDistinct()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT Sum(DISTINCT amount) FROM orders");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Single(selectStmt.Columns);
        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[0]);
        Assert.Equal("Sum", aggColumn.AggregateName);
        Assert.True(aggColumn.IsDistinct);
        Assert.NotNull(aggColumn.Argument);
    }

    [Fact]
    public void Select_AvgDistinct()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT Avg(DISTINCT score) FROM results");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Single(selectStmt.Columns);
        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[0]);
        Assert.Equal("Avg", aggColumn.AggregateName);
        Assert.True(aggColumn.IsDistinct);
        Assert.NotNull(aggColumn.Argument);
    }

    [Fact]
    public void Select_MinDistinct()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT Min(DISTINCT price) FROM products");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Single(selectStmt.Columns);
        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[0]);
        Assert.Equal("Min", aggColumn.AggregateName);
        Assert.True(aggColumn.IsDistinct);
        Assert.NotNull(aggColumn.Argument);
    }

    [Fact]
    public void Select_MaxDistinct()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT Max(DISTINCT salary) FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Single(selectStmt.Columns);
        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[0]);
        Assert.Equal("Max", aggColumn.AggregateName);
        Assert.True(aggColumn.IsDistinct);
        Assert.NotNull(aggColumn.Argument);
    }

    [Fact]
    public void Select_CountWithoutDistinct_IsDistinctFalse()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT COUNT(category) FROM products");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[0]);
        Assert.Equal("COUNT", aggColumn.AggregateName);
        Assert.False(aggColumn.IsDistinct);
        Assert.NotNull(aggColumn.Argument);
    }

    [Fact]
    public void Select_CountStarWithoutDistinct_IsDistinctFalse()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT COUNT(*) FROM products");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[0]);
        Assert.Equal("COUNT", aggColumn.AggregateName);
        Assert.False(aggColumn.IsDistinct);
        Assert.Null(aggColumn.Argument);
    }

    [Fact]
    public void Select_CountDistinct_WithAlias()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT COUNT(DISTINCT category) AS unique_categories FROM products");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[0]);
        Assert.Equal("COUNT", aggColumn.AggregateName);
        Assert.True(aggColumn.IsDistinct);
        Assert.NotNull(aggColumn.Argument);
        Assert.Equal("unique_categories", aggColumn.ColumnAlias);
    }

    [Fact]
    public void Select_CountDistinct_WithWindowFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT COUNT(DISTINCT category) OVER (PARTITION BY region) AS dist_count FROM products");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        var aggColumn = Assert.IsType<SqlAggregate>(selectStmt.Columns[0]);
        Assert.Equal("COUNT", aggColumn.AggregateName);
        Assert.True(aggColumn.IsDistinct);
        Assert.True(aggColumn.IsWindowFunction);
        Assert.NotNull(aggColumn.WindowSpecification);
        Assert.Single(aggColumn.WindowSpecification!.PartitionBy);
    }

    [Fact]
    public void Select_MultipleDistinctAggregates()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT COUNT(DISTINCT category) AS cat_count, Sum(DISTINCT amount) AS total FROM orders");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Equal(2, selectStmt.Columns.Count);

        var countCol = Assert.IsType<SqlAggregate>(selectStmt.Columns[0]);
        Assert.Equal("COUNT", countCol.AggregateName);
        Assert.True(countCol.IsDistinct);

        var sumCol = Assert.IsType<SqlAggregate>(selectStmt.Columns[1]);
        Assert.Equal("Sum", sumCol.AggregateName);
        Assert.True(sumCol.IsDistinct);
    }

    // ── GROUP BY with ROLLUP / CUBE / GROUPING SETS (#46) ───────────────

    [Fact]
    public void Select_GroupBy_SimpleColumns()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT department, COUNT(*) FROM employees GROUP BY department");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        Assert.Single(selectStmt.GroupBy!.Columns);
        Assert.Equal("department", selectStmt.GroupBy.Columns[0]);
        Assert.Empty(selectStmt.GroupBy.GroupingSets);
    }

    [Fact]
    public void Select_GroupBy_MultipleColumns()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT department, region, COUNT(*) FROM employees GROUP BY department, region");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        Assert.Equal(2, selectStmt.GroupBy!.Columns.Count);
        Assert.Equal("department", selectStmt.GroupBy.Columns[0]);
        Assert.Equal("region", selectStmt.GroupBy.Columns[1]);
    }

    [Fact]
    public void Select_GroupBy_Rollup()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT department, region, COUNT(*) FROM employees GROUP BY ROLLUP(department, region)");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        Assert.Empty(selectStmt.GroupBy!.Columns);
        Assert.Single(selectStmt.GroupBy.GroupingSets);

        var rollup = selectStmt.GroupBy.GroupingSets[0];
        Assert.Equal(GroupingSetType.Rollup, rollup.Type);
        Assert.Equal(2, rollup.Sets.Count);
        Assert.Equal("department", rollup.Sets[0][0]);
        Assert.Equal("region", rollup.Sets[1][0]);
    }

    [Fact]
    public void Select_GroupBy_Cube()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT department, region, COUNT(*) FROM employees GROUP BY CUBE(department, region)");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        Assert.Empty(selectStmt.GroupBy!.Columns);
        Assert.Single(selectStmt.GroupBy.GroupingSets);

        var cube = selectStmt.GroupBy.GroupingSets[0];
        Assert.Equal(GroupingSetType.Cube, cube.Type);
        Assert.Equal(2, cube.Sets.Count);
        Assert.Equal("department", cube.Sets[0][0]);
        Assert.Equal("region", cube.Sets[1][0]);
    }

    [Fact]
    public void Select_GroupBy_GroupingSets()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT department, region, COUNT(*) FROM employees GROUP BY GROUPING SETS((department), (region), ())");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        Assert.Empty(selectStmt.GroupBy!.Columns);
        Assert.Single(selectStmt.GroupBy.GroupingSets);

        var gs = selectStmt.GroupBy.GroupingSets[0];
        Assert.Equal(GroupingSetType.GroupingSets, gs.Type);
        Assert.Equal(3, gs.Sets.Count);

        // First set: (department)
        Assert.Single(gs.Sets[0]);
        Assert.Equal("department", gs.Sets[0][0]);

        // Second set: (region)
        Assert.Single(gs.Sets[1]);
        Assert.Equal("region", gs.Sets[1][0]);

        // Third set: () — empty grouping set
        Assert.Empty(gs.Sets[2]);
    }

    [Fact]
    public void Select_GroupBy_GroupingSets_MultipleColumnsInSet()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT department, region, city, COUNT(*) FROM employees " +
            "GROUP BY GROUPING SETS((department, region), (city), ())");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        var gs = selectStmt.GroupBy!.GroupingSets[0];
        Assert.Equal(GroupingSetType.GroupingSets, gs.Type);
        Assert.Equal(3, gs.Sets.Count);

        // First set: (department, region)
        Assert.Equal(2, gs.Sets[0].Count);
        Assert.Equal("department", gs.Sets[0][0]);
        Assert.Equal("region", gs.Sets[0][1]);

        // Second set: (city)
        Assert.Single(gs.Sets[1]);
        Assert.Equal("city", gs.Sets[1][0]);

        // Third set: ()
        Assert.Empty(gs.Sets[2]);
    }

    [Fact]
    public void Select_GroupBy_Rollup_SingleColumn()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT year, SUM(sales) FROM revenue GROUP BY ROLLUP(year)");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        var rollup = selectStmt.GroupBy!.GroupingSets[0];
        Assert.Equal(GroupingSetType.Rollup, rollup.Type);
        Assert.Single(rollup.Sets);
        Assert.Equal("year", rollup.Sets[0][0]);
    }

    [Fact]
    public void Select_GroupBy_WithHaving()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT department, COUNT(*) FROM employees GROUP BY department HAVING department > 5");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        Assert.Single(selectStmt.GroupBy!.Columns);
        Assert.Equal("department", selectStmt.GroupBy.Columns[0]);
        Assert.NotNull(selectStmt.HavingClause);
    }

    [Fact]
    public void Select_NoGroupBy_GroupByIsNull()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT * FROM employees");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.Null(selectStmt.GroupBy);
        Assert.Null(selectStmt.HavingClause);
    }

    [Fact]
    public void Select_GroupBy_Cube_ThreeColumns()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT department, region, year, SUM(sales) FROM revenue " +
            "GROUP BY CUBE(department, region, year)");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        var cube = selectStmt.GroupBy!.GroupingSets[0];
        Assert.Equal(GroupingSetType.Cube, cube.Type);
        Assert.Equal(3, cube.Sets.Count);
        Assert.Equal("department", cube.Sets[0][0]);
        Assert.Equal("region", cube.Sets[1][0]);
        Assert.Equal("year", cube.Sets[2][0]);
    }

    [Fact]
    public void Select_GroupBy_ColumnAndRollup()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT year, quarter, region, SUM(sales) FROM revenue " +
            "GROUP BY year, ROLLUP(quarter, region)");

        var selectStmt = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectStmt.GroupBy);
        Assert.Single(selectStmt.GroupBy!.Columns);
        Assert.Equal("year", selectStmt.GroupBy.Columns[0]);
        Assert.Single(selectStmt.GroupBy.GroupingSets);

        var rollup = selectStmt.GroupBy.GroupingSets[0];
        Assert.Equal(GroupingSetType.Rollup, rollup.Type);
        Assert.Equal(2, rollup.Sets.Count);
        Assert.Equal("quarter", rollup.Sets[0][0]);
        Assert.Equal("region", rollup.Sets[1][0]);
    }

}
