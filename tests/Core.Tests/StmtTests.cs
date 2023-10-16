using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class StmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SelectStmt selectStmt = new(this);
            Expr expr = selectStmt.JoinChainOpt.Expr;

            expr.InitializeRule(selectStmt, selectStmt.FuncCall);

            InsertStmt insertStmt = new(this, selectStmt);
            UpdateStmt updateStmt = new(this, selectStmt.TableName, selectStmt.FuncCall, selectStmt.WhereClauseOpt);
            DeleteStmt deleteStmt = new(this, selectStmt.TableName, selectStmt.WhereClauseOpt, updateStmt.ReturningClauseOpt);
            CreateTableStmt createTableStmt = new(this, selectStmt.Id);

            Stmt stmt = new(this, selectStmt, insertStmt, updateStmt, deleteStmt, createTableStmt);
            Root = stmt;
        }

        public virtual SqlDefinition Create(ParseTreeNode stmt, IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider) =>
            ((Stmt)Root).Create(stmt, databaseConnectionProvider, tableSchemaProvider);
    }

    [Fact]
    public void Stmt_Select()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT ID, CustomerName FROM Customers");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var sqlDefinition = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert - Only need to assert that the correct type of statement was created.  Each statement type has its own unit tests to test their individual details.
        Assert.NotNull(sqlDefinition.Select);
        Assert.Null(sqlDefinition.Insert);
        Assert.Null(sqlDefinition.Update);
        Assert.Null(sqlDefinition.Delete);
        Assert.Null(sqlDefinition.Create);
    }

    [Fact]
    public void Stmt_Insert()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "INSERT INTO locations (city, state, zip) VALUES ('Boston', 'MA', 90001)");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var sqlDefinition = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert - Only need to assert that the correct type of statement was created.  Each statement type has its own unit tests to test their individual details.
        Assert.Null(sqlDefinition.Select);
        Assert.NotNull(sqlDefinition.Insert);
        Assert.Null(sqlDefinition.Update);
        Assert.Null(sqlDefinition.Delete);
        Assert.Null(sqlDefinition.Create);
    }

    [Fact]
    public void Stmt_Update()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE locations SET zip = 32655 WHERE city = 'Boston'");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var sqlDefinition = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert - Only need to assert that the correct type of statement was created.  Each statement type has its own unit tests to test their individual details.
        Assert.Null(sqlDefinition.Select);
        Assert.Null(sqlDefinition.Insert);
        Assert.NotNull(sqlDefinition.Update);
        Assert.Null(sqlDefinition.Delete);
        Assert.Null(sqlDefinition.Create);
    }

    [Fact]
    public void Stmt_Delete()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "DELETE FROM employees WHERE name='Joe'");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var sqlDefinition = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert - Only need to assert that the correct type of statement was created.  Each statement type has its own unit tests to test their individual details.
        Assert.Null(sqlDefinition.Select);
        Assert.Null(sqlDefinition.Insert);
        Assert.Null(sqlDefinition.Update);
        Assert.NotNull(sqlDefinition.Delete);
        Assert.Null(sqlDefinition.Create);
    }

    [Fact]
    public void Stmt_CreateTable()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "CREATE TABLE Example (ID INT PRIMARY KEY)");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var sqlDefinition = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert - Only need to assert that the correct type of statement was created.  Each statement type has its own unit tests to test their individual details.
        Assert.Null(sqlDefinition.Select);
        Assert.Null(sqlDefinition.Insert);
        Assert.Null(sqlDefinition.Update);
        Assert.Null(sqlDefinition.Delete);
        Assert.NotNull(sqlDefinition.Create);
    }

}
