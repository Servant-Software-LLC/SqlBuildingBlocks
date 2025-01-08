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
}
