using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class StmtLineTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            StmtLine stmtLine = new(this);
            Root = stmtLine;
        }

        public virtual IEnumerable<SqlDefinition> Create(ParseTreeNode stmtList) =>
            ((StmtLine)Root).Create(stmtList);

        public virtual IEnumerable<SqlDefinition> Create(ParseTreeNode stmtList, IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider,
                                        IFunctionProvider functionProvider = null) =>
            ((StmtLine)Root).Create(stmtList, databaseConnectionProvider, tableSchemaProvider, functionProvider);
            
    }

    [Fact]
    public void SingleLine_EndingInSemiColon()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT ID, CustomerName FROM Customers;");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var sqlDefinitions = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider).ToList();

        Assert.Single(sqlDefinitions);
        var sqlDefinition = sqlDefinitions[0];

        //Assert - Only need to assert that the correct type of statement was created.  Each statement type has its own unit tests to test their individual details.
        Assert.NotNull(sqlDefinition.Select);
        Assert.Null(sqlDefinition.Insert);
        Assert.Null(sqlDefinition.Update);
        Assert.Null(sqlDefinition.Delete);
        Assert.Null(sqlDefinition.Create);
        Assert.Null(sqlDefinition.Alter);
    }

    [Fact]
    public void MultipleLines()
    {
        TestGrammar grammar = new();
        var stmtList = GrammarParser.Parse(grammar, "SELECT ID, CustomerName FROM Customers;INSERT INTO locations (city, state, zip) VALUES ('Boston', 'MA', 90001)");

        var sqlDefinitions = grammar.Create(stmtList).ToList();

        Assert.Equal(2, sqlDefinitions.Count);
        var selectSqlDefinition = sqlDefinitions[0];

        //Assert - Only need to assert that the correct type of statement was created.  Each statement type has its own unit tests to test their individual details.
        Assert.NotNull(selectSqlDefinition.Select);
        Assert.Null(selectSqlDefinition.Insert);
        Assert.Null(selectSqlDefinition.Update);
        Assert.Null(selectSqlDefinition.Delete);
        Assert.Null(selectSqlDefinition.Create);
        Assert.Null(selectSqlDefinition.Alter);

        var insertSqlDefinition = sqlDefinitions[1];

        //Assert - Only need to assert that the correct type of statement was created.  Each statement type has its own unit tests to test their individual details.
        Assert.Null(insertSqlDefinition.Select);
        Assert.NotNull(insertSqlDefinition.Insert);
        Assert.Null(insertSqlDefinition.Update);
        Assert.Null(insertSqlDefinition.Delete);
        Assert.Null(insertSqlDefinition.Create);
        Assert.Null(insertSqlDefinition.Alter);
    }
}
