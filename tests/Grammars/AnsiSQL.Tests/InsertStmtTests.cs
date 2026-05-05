using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.AnsiSQL.Tests;

public class InsertStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar() : base(false) // SQL is case insensitive
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
            AnsiSQL.SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            InsertStmt insertStmt = new(this, id, expr, selectStmt);

            Root = insertStmt;
        }

        public virtual SqlInsertDefinition Create(ParseTreeNode insertStmt) =>
            ((InsertStmt)Root).Create(insertStmt);

        public virtual SqlInsertDefinition Create(ParseTreeNode insertStmt,
            IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider) =>
            ((InsertStmt)Root).Create(insertStmt, databaseConnectionProvider, tableSchemaProvider);
    }

    [Fact]
    public void Insert_WithValues_SingleRow()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO locations (city, state, zip) VALUES ('Boston', 'MA', 90001)");

        var sqlInsertDefinition = grammar.Create(node);

        Assert.NotNull(sqlInsertDefinition.Table);
        Assert.Equal("locations", sqlInsertDefinition.Table.TableName);

        Assert.Equal(3, sqlInsertDefinition.Columns.Count);
        Assert.Equal("city", sqlInsertDefinition.Columns[0].ColumnName);
        Assert.Equal("state", sqlInsertDefinition.Columns[1].ColumnName);
        Assert.Equal("zip", sqlInsertDefinition.Columns[2].ColumnName);

        Assert.NotNull(sqlInsertDefinition.Values);
        Assert.Single(sqlInsertDefinition.Values);

        var row = sqlInsertDefinition.Values[0];
        Assert.Equal(3, row.Count);
        Assert.Equal("Boston", row[0].Value!.String);
        Assert.Equal("MA", row[1].Value!.String);
        Assert.Equal(90001, row[2].Value!.Int);
    }

    [Fact]
    public void Insert_WithValues_MultipleRows()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO locations (city, state, zip) VALUES ('Boston', 'MA', 90001), ('New York', 'NY', 10001)");

        var sqlInsertDefinition = grammar.Create(node);

        Assert.Equal("locations", sqlInsertDefinition.Table!.TableName);
        Assert.NotNull(sqlInsertDefinition.Values);
        Assert.Equal(2, sqlInsertDefinition.Values.Count);

        Assert.Equal("Boston", sqlInsertDefinition.Values[0][0].Value!.String);
        Assert.Equal("New York", sqlInsertDefinition.Values[1][0].Value!.String);
    }

    [Fact]
    public void Insert_FromSelect()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO archive (id, description) SELECT id, description FROM source_table");

        // Using the no-provider Create overload — we are asserting on AST shape,
        // not on resolved table/column references.
        var sqlInsertDefinition = grammar.Create(node);

        Assert.NotNull(sqlInsertDefinition.Table);
        Assert.Equal("archive", sqlInsertDefinition.Table.TableName);

        Assert.Equal(2, sqlInsertDefinition.Columns.Count);
        Assert.Equal("id", sqlInsertDefinition.Columns[0].ColumnName);
        Assert.Equal("description", sqlInsertDefinition.Columns[1].ColumnName);

        Assert.NotNull(sqlInsertDefinition.SelectDefinition);
        Assert.Equal("source_table", sqlInsertDefinition.SelectDefinition.Table!.TableName);
    }

    [Fact]
    public void Insert_WithSingleColumn()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "INSERT INTO logs (message) VALUES ('Started')");

        var sqlInsertDefinition = grammar.Create(node);

        Assert.Equal("logs", sqlInsertDefinition.Table!.TableName);
        Assert.Single(sqlInsertDefinition.Columns);
        Assert.Equal("message", sqlInsertDefinition.Columns[0].ColumnName);
        Assert.Single(sqlInsertDefinition.Values!);
        Assert.Equal("Started", sqlInsertDefinition.Values![0][0].Value!.String);
    }
}
