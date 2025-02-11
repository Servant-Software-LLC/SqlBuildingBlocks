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
        var whereLeftExpr = selectStmt.WhereClause.Left.BinExpr;
        Assert.NotNull(whereLeftExpr);
        Assert.NotNull(whereLeftExpr.Left.Function);
        Assert.Equal("ROW_COUNT", whereLeftExpr.Left.Function.FunctionName);
        Assert.Empty(whereLeftExpr.Left.Function.Arguments);
        Assert.NotNull(whereLeftExpr.Right.Value);
        Assert.Equal(1, whereLeftExpr.Right.Value.Int);

        //WHERE - BlogId=LAST_INSERT_ID()
        var whereRightExpr = selectStmt.WhereClause.Right.BinExpr;
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

}
