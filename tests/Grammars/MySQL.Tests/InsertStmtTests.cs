using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.MySQL.Tests;

public class InsertStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
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
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            MySQL.InsertStmt insertStmt = new(this, id, expr, selectStmt);

            Root = insertStmt;
        }

        public SqlInsertDefinition Create(ParseTreeNode insertStmt) =>
            ((MySQL.InsertStmt)Root).Create(insertStmt);
    }

    [Fact]
    public void Insert_OnDuplicateKeyUpdate()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name, email) VALUES (1, 'Alice', 'alice@example.com') ON DUPLICATE KEY UPDATE name = 'Alice Updated', email = 'newalice@example.com'");

        var sqlInsertDefinition = grammar.Create(node);

        // Assert on the Table
        Assert.NotNull(sqlInsertDefinition.Table);
        Assert.Equal("users", sqlInsertDefinition.Table.TableName);

        // Assert on the Columns
        Assert.Equal(3, sqlInsertDefinition.Columns.Count);
        Assert.Equal("id", sqlInsertDefinition.Columns[0].ColumnName);
        Assert.Equal("name", sqlInsertDefinition.Columns[1].ColumnName);
        Assert.Equal("email", sqlInsertDefinition.Columns[2].ColumnName);

        // Assert on VALUES
        Assert.NotNull(sqlInsertDefinition.Values);
        Assert.Single(sqlInsertDefinition.Values);

        // Assert on UpsertClause
        Assert.NotNull(sqlInsertDefinition.UpsertClause);
        Assert.Equal(SqlUpsertAction.Update, sqlInsertDefinition.UpsertClause.Action);
        Assert.Empty(sqlInsertDefinition.UpsertClause.ConflictColumns);

        // Assert on assignments
        Assert.Equal(2, sqlInsertDefinition.UpsertClause.Assignments.Count);

        var assignment1 = sqlInsertDefinition.UpsertClause.Assignments[0];
        Assert.Equal("name", assignment1.Column.ColumnName);
        Assert.NotNull(assignment1.Value);
        Assert.Equal("Alice Updated", assignment1.Value.String);

        var assignment2 = sqlInsertDefinition.UpsertClause.Assignments[1];
        Assert.Equal("email", assignment2.Column.ColumnName);
        Assert.NotNull(assignment2.Value);
        Assert.Equal("newalice@example.com", assignment2.Value.String);
    }

    [Fact]
    public void Insert_WithoutOnDuplicateKey()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name) VALUES (1, 'Alice')");

        var sqlInsertDefinition = grammar.Create(node);

        Assert.NotNull(sqlInsertDefinition.Table);
        Assert.Equal("users", sqlInsertDefinition.Table.TableName);
        Assert.Null(sqlInsertDefinition.UpsertClause);
    }

    [Fact]
    public void Insert_OnDuplicateKeyUpdate_WithExpression()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO counters (id, count) VALUES (1, 1) ON DUPLICATE KEY UPDATE count = count + 1");

        var sqlInsertDefinition = grammar.Create(node);

        Assert.NotNull(sqlInsertDefinition.UpsertClause);
        Assert.Equal(SqlUpsertAction.Update, sqlInsertDefinition.UpsertClause.Action);
        Assert.Single(sqlInsertDefinition.UpsertClause.Assignments);

        var assignment = sqlInsertDefinition.UpsertClause.Assignments[0];
        Assert.Equal("count", assignment.Column.ColumnName);
        // The expression is a binary expression (count + 1)
        Assert.NotNull(assignment.Expression);
    }
}
