using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class InsertStmtTests
{
    private class TestGrammar : Grammar
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
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            InsertStmt insertStmt = new(this, id, expr, selectStmt);

            Root = insertStmt;
        }

        public virtual SqlInsertDefinition Create(ParseTreeNode insertStmt, IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider) =>
            ((InsertStmt)Root).Create(insertStmt, databaseConnectionProvider, tableSchemaProvider);
    }

    [Fact]
    public void Insert_WithValues()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "INSERT INTO locations (city, state, zip) VALUES ('Boston', 'MA', 90001)");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var sqlInsertDefinition = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert on the Table
        Assert.NotNull(sqlInsertDefinition.Table);
        Assert.Equal("locations", sqlInsertDefinition.Table.TableName);

        //Assert on the Columns
        Assert.Equal(3, sqlInsertDefinition.Columns.Count);
        Assert.Equal("city", sqlInsertDefinition.Columns[0].ColumnName);
        Assert.Equal("state", sqlInsertDefinition.Columns[1].ColumnName);
        Assert.Equal("zip", sqlInsertDefinition.Columns[2].ColumnName);

        //Assert on VALUES
        Assert.NotNull(sqlInsertDefinition.Values);
        Assert.Equal(3, sqlInsertDefinition.Values.Count);
        Assert.NotNull(sqlInsertDefinition.Values[0].Value);
        Assert.Equal("Boston", sqlInsertDefinition.Values[0].Value.String);
        Assert.NotNull(sqlInsertDefinition.Values[1].Value);
        Assert.Equal("MA", sqlInsertDefinition.Values[1].Value.String);
        Assert.NotNull(sqlInsertDefinition.Values[2].Value);
        Assert.Equal(90001, sqlInsertDefinition.Values[2].Value.Int);
    }


    [Fact]
    public void Insert_WithSelect()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "INSERT INTO locations (city, state, zip) SELECT Name, Name, ID FROM Products");

        DatabaseConnectionProvider databaseConnectionProvider = new();
        TableSchemaProvider tableSchemaProvider = new();
        var insertStmt = grammar.Create(node, databaseConnectionProvider, tableSchemaProvider);

        //Assert on the Table
        Assert.NotNull(insertStmt.Table);
        Assert.Equal("locations", insertStmt.Table.TableName);

        //Assert on the Columns
        Assert.Equal(3, insertStmt.Columns.Count);
        Assert.Equal("city", insertStmt.Columns[0].ColumnName);
        Assert.Equal("state", insertStmt.Columns[1].ColumnName);
        Assert.Equal("zip", insertStmt.Columns[2].ColumnName);

        //Assert on SELECT  (Most of the asserts are tested in SelectStmtTests)
        Assert.NotNull(insertStmt.SelectDefinition);

    }

}
