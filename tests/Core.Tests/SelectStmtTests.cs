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

}
