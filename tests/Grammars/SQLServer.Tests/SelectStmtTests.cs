using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.SQLServer.Tests;

public class SelectStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            //SQLServer has special naming rules for identifiers.  (Note: the backtick)
            SQLServer.SimpleId simpleId = new(this);

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
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = selectStmt;
        }

        public virtual SqlSelectDefinition Create(ParseTreeNode selectStmt) =>
            ((SelectStmt)Root).Create(selectStmt);

        public virtual SqlSelectDefinition Create(ParseTreeNode selectStmt, IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider) =>
            ((SelectStmt)Root).Create(selectStmt, databaseConnectionProvider, tableSchemaProvider);
    }

    [Fact]
    public void Select_With_DoubleQuotes()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT \"BlogId\"\r\nFROM \"Blogs\"");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert on select columns
        Assert.Single(selectStmt.Columns);

        //First column - Name "BlogId"
        Assert.IsType<SqlColumn>(selectStmt.Columns[0]);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("BlogId", firstColumn.ColumnName);
        Assert.NotNull(firstColumn.TableRef);
        Assert.Equal("Blogs", firstColumn.TableRef.TableName);

        //FROM - Name "Blogs"
        Assert.Equal("Blogs", selectStmt.Table.TableName);


        //No JOINs
        Assert.Empty(selectStmt.Joins);

        //WHERE
        Assert.Null(selectStmt.WhereClause);

        //LIMIT
        Assert.Null(selectStmt.Limit);
    }

    [Fact]
    public void Select_ResolveReferences_ColumnsWithSquareBrackets()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT c.CustomerName, [o].[OrderDate], [oi].[Quantity], [p].[Name] FROM [Customers] c INNER JOIN [Orders] o ON [c].[ID] = [o].[CustomerID] INNER JOIN [OrderItems] oi ON [o].[ID] = [oi].[OrderID] INNER JOIN [Products] p ON [p].[ID] = [oi].[ProductID]");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node);

        //Act
        selectStmt.ResolveReferences(databaseConnectionProvider, tableSchemaProvider);

        //Assert
        Assert.False(selectStmt.InvalidReferences, $"Unable to resolve the references with the SELECT statement.  Reason: {selectStmt.InvalidReferenceReason}");
    }

    [Fact]
    public void Select_Where_Exists_WithSquareBracketIdentifiers()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT [c].[CustomerName] FROM [Customers] c WHERE EXISTS (SELECT [o].[ID] FROM [Orders] o WHERE [o].[CustomerID] = [c].[ID])");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.False(selectStmt.InvalidReferences, $"Unable to resolve the references with the SELECT statement.  Reason: {selectStmt.InvalidReferenceReason}");
        Assert.NotNull(selectStmt.WhereClause?.ExistsExpr);
    }

    [Fact]
    public void Select_BracketQuoted_ColumnNames()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT [CustomerName] FROM Customers");

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
    public void Select_BracketQuoted_TableName()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT CustomerName FROM [Customers]");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.Single(selectStmt.Columns);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("CustomerName", firstColumn.ColumnName);
        Assert.Equal("Customers", selectStmt.Table.TableName);
    }

    [Fact]
    public void Select_BracketQuoted_ColumnsAndTable()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT [ID], [CustomerName] FROM [Customers]");

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
    public void Select_BracketQuoted_DotSeparatedIdentifiers()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT [c].[CustomerName] FROM [Customers] c");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("CustomerName", firstColumn.ColumnName);
        Assert.Equal("c", firstColumn.TableName);
        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.Equal("c", selectStmt.Table.TableAlias);
    }

    [Fact]
    public void Select_BracketQuoted_TableAlias()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT [c].[ID] FROM [Customers] AS [c]");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("ID", firstColumn.ColumnName);
        Assert.Equal("c", firstColumn.TableName);
        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.Equal("c", selectStmt.Table.TableAlias);
    }

    [Fact]
    public void Select_BracketQuoted_WithJoin()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT [c].[CustomerName], [o].[OrderDate] FROM [Customers] [c] INNER JOIN [Orders] [o] ON [c].[ID] = [o].[CustomerID]");

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
    public void Select_BracketQuoted_WithWhere()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "SELECT [ID] FROM [Customers] WHERE [CustomerName] = 'Alice'");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        Assert.Equal("Customers", selectStmt.Table.TableName);
        Assert.NotNull(selectStmt.WhereClause);
    }

    [Fact]
    public void Select_BracketQuoted_ReservedWordAsIdentifier()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT [Order], [Select], [From] FROM [Table]");

        var selectStmt = grammar.Create(node);

        Assert.Equal(3, selectStmt.Columns.Count);
        Assert.Equal("Order", (selectStmt.Columns[0] as SqlColumn).ColumnName);
        Assert.Equal("Select", (selectStmt.Columns[1] as SqlColumn).ColumnName);
        Assert.Equal("From", (selectStmt.Columns[2] as SqlColumn).ColumnName);
        Assert.Equal("Table", selectStmt.Table.TableName);
    }

    [Fact]
    public void Select_ColumnAlias_With_Brackets()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT Name [First Name] FROM Products");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var selectStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        Assert.Single(selectStmt.Columns);
        Assert.IsType<SqlColumn>(selectStmt.Columns[0]);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("Name", firstColumn.ColumnName);
        Assert.NotNull(firstColumn.TableRef);
        Assert.Equal("Products", firstColumn.TableRef.TableName);
        Assert.Equal("First Name", firstColumn.ColumnAlias);
    }

    [Fact]
    public void Select_BracketQuoted_ThreePartIdentifier()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT [dbo].[Customers].[CustomerName] FROM [dbo].[Customers]");

        var selectStmt = grammar.Create(node);

        Assert.Single(selectStmt.Columns);
        var firstColumn = selectStmt.Columns[0] as SqlColumn;
        Assert.Equal("CustomerName", firstColumn.ColumnName);
    }
}
