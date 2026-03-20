using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.PostgreSQL.Tests;

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

            PostgreSQL.InsertStmt insertStmt = new(this, id, expr, selectStmt);

            Root = insertStmt;
        }

        public SqlInsertDefinition Create(ParseTreeNode insertStmt) =>
            ((PostgreSQL.InsertStmt)Root).Create(insertStmt);
    }

    [Fact]
    public void Insert_OnConflictDoNothing()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name, email) VALUES (1, 'Alice', 'alice@example.com') ON CONFLICT (id) DO NOTHING");

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
        Assert.Equal(SqlUpsertAction.DoNothing, sqlInsertDefinition.UpsertClause.Action);

        // Assert on conflict columns
        Assert.Single(sqlInsertDefinition.UpsertClause.ConflictColumns);
        Assert.Equal("id", sqlInsertDefinition.UpsertClause.ConflictColumns[0].ColumnName);

        // No assignments for DO NOTHING
        Assert.Empty(sqlInsertDefinition.UpsertClause.Assignments);
    }

    [Fact]
    public void Insert_OnConflictDoUpdateSet()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name, email) VALUES (1, 'Alice', 'alice@example.com') ON CONFLICT (id) DO UPDATE SET name = 'Alice Updated', email = 'newalice@example.com'");

        var sqlInsertDefinition = grammar.Create(node);

        // Assert on the Table
        Assert.NotNull(sqlInsertDefinition.Table);
        Assert.Equal("users", sqlInsertDefinition.Table.TableName);

        // Assert on UpsertClause
        Assert.NotNull(sqlInsertDefinition.UpsertClause);
        Assert.Equal(SqlUpsertAction.Update, sqlInsertDefinition.UpsertClause.Action);

        // Assert on conflict columns
        Assert.Single(sqlInsertDefinition.UpsertClause.ConflictColumns);
        Assert.Equal("id", sqlInsertDefinition.UpsertClause.ConflictColumns[0].ColumnName);

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
    public void Insert_OnConflictMultipleColumns()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO inventory (warehouse_id, product_id, quantity) VALUES (1, 100, 50) ON CONFLICT (warehouse_id, product_id) DO UPDATE SET quantity = 50");

        var sqlInsertDefinition = grammar.Create(node);

        Assert.NotNull(sqlInsertDefinition.UpsertClause);
        Assert.Equal(SqlUpsertAction.Update, sqlInsertDefinition.UpsertClause.Action);

        // Assert on multiple conflict columns
        Assert.Equal(2, sqlInsertDefinition.UpsertClause.ConflictColumns.Count);
        Assert.Equal("warehouse_id", sqlInsertDefinition.UpsertClause.ConflictColumns[0].ColumnName);
        Assert.Equal("product_id", sqlInsertDefinition.UpsertClause.ConflictColumns[1].ColumnName);

        // Assert on assignment
        Assert.Single(sqlInsertDefinition.UpsertClause.Assignments);
        Assert.Equal("quantity", sqlInsertDefinition.UpsertClause.Assignments[0].Column.ColumnName);
    }

    [Fact]
    public void Insert_WithoutOnConflict()
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
    public void Insert_OnConflictDoUpdateSet_WithExpression()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO counters (id, count) VALUES (1, 1) ON CONFLICT (id) DO UPDATE SET count = count + 1");

        var sqlInsertDefinition = grammar.Create(node);

        Assert.NotNull(sqlInsertDefinition.UpsertClause);
        Assert.Equal(SqlUpsertAction.Update, sqlInsertDefinition.UpsertClause.Action);

        Assert.Single(sqlInsertDefinition.UpsertClause.ConflictColumns);
        Assert.Equal("id", sqlInsertDefinition.UpsertClause.ConflictColumns[0].ColumnName);

        Assert.Single(sqlInsertDefinition.UpsertClause.Assignments);
        var assignment = sqlInsertDefinition.UpsertClause.Assignments[0];
        Assert.Equal("count", assignment.Column.ColumnName);
        Assert.NotNull(assignment.Expression);
    }

    [Fact]
    public void Insert_OnConflictDoUpdateSet_WithExcluded()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name, email) VALUES (1, 'Alice', 'alice@example.com') ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, email = EXCLUDED.email");

        var sqlInsertDefinition = grammar.Create(node);

        Assert.NotNull(sqlInsertDefinition.UpsertClause);
        Assert.Equal(SqlUpsertAction.Update, sqlInsertDefinition.UpsertClause.Action);

        Assert.Single(sqlInsertDefinition.UpsertClause.ConflictColumns);
        Assert.Equal("id", sqlInsertDefinition.UpsertClause.ConflictColumns[0].ColumnName);

        // Assert on assignments referencing EXCLUDED
        Assert.Equal(2, sqlInsertDefinition.UpsertClause.Assignments.Count);

        var assignment1 = sqlInsertDefinition.UpsertClause.Assignments[0];
        Assert.Equal("name", assignment1.Column.ColumnName);
        // EXCLUDED.name is parsed as a column reference with table "EXCLUDED"
        Assert.NotNull(assignment1.Expression.Column);
        Assert.Equal("EXCLUDED", assignment1.Expression.Column.TableName);
        Assert.Equal("name", assignment1.Expression.Column.ColumnName);

        var assignment2 = sqlInsertDefinition.UpsertClause.Assignments[1];
        Assert.Equal("email", assignment2.Column.ColumnName);
        Assert.NotNull(assignment2.Expression.Column);
        Assert.Equal("EXCLUDED", assignment2.Expression.Column.TableName);
        Assert.Equal("email", assignment2.Expression.Column.ColumnName);
    }

    [Fact]
    public void Insert_OnConflictDoNothing_WithoutTarget()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name) VALUES (1, 'Alice') ON CONFLICT DO NOTHING");

        var sqlInsertDefinition = grammar.Create(node);

        Assert.NotNull(sqlInsertDefinition.UpsertClause);
        Assert.Equal(SqlUpsertAction.DoNothing, sqlInsertDefinition.UpsertClause.Action);

        // No conflict columns when no target specified
        Assert.Empty(sqlInsertDefinition.UpsertClause.ConflictColumns);
        Assert.Null(sqlInsertDefinition.UpsertClause.ConstraintName);
        Assert.Empty(sqlInsertDefinition.UpsertClause.Assignments);
    }

    [Fact]
    public void Insert_OnConflictOnConstraint_DoNothing()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name) VALUES (1, 'Alice') ON CONFLICT ON CONSTRAINT users_pkey DO NOTHING");

        var sqlInsertDefinition = grammar.Create(node);

        Assert.NotNull(sqlInsertDefinition.UpsertClause);
        Assert.Equal(SqlUpsertAction.DoNothing, sqlInsertDefinition.UpsertClause.Action);

        // Constraint name should be set
        Assert.Equal("users_pkey", sqlInsertDefinition.UpsertClause.ConstraintName);

        // No conflict columns when using constraint
        Assert.Empty(sqlInsertDefinition.UpsertClause.ConflictColumns);
        Assert.Empty(sqlInsertDefinition.UpsertClause.Assignments);
    }

    [Fact]
    public void Insert_OnConflictOnConstraint_DoUpdateSet()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name, email) VALUES (1, 'Alice', 'alice@example.com') ON CONFLICT ON CONSTRAINT users_pkey DO UPDATE SET name = EXCLUDED.name");

        var sqlInsertDefinition = grammar.Create(node);

        Assert.NotNull(sqlInsertDefinition.UpsertClause);
        Assert.Equal(SqlUpsertAction.Update, sqlInsertDefinition.UpsertClause.Action);

        Assert.Equal("users_pkey", sqlInsertDefinition.UpsertClause.ConstraintName);
        Assert.Empty(sqlInsertDefinition.UpsertClause.ConflictColumns);

        Assert.Single(sqlInsertDefinition.UpsertClause.Assignments);
        var assignment = sqlInsertDefinition.UpsertClause.Assignments[0];
        Assert.Equal("name", assignment.Column.ColumnName);
        Assert.NotNull(assignment.Expression.Column);
        Assert.Equal("EXCLUDED", assignment.Expression.Column.TableName);
        Assert.Equal("name", assignment.Expression.Column.ColumnName);
    }

    [Fact]
    public void Insert_OnConflictDoUpdateSet_WithWhereClause()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name, email) VALUES (1, 'Alice', 'alice@example.com') ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name WHERE users.id > 0");

        var sqlInsertDefinition = grammar.Create(node);

        Assert.NotNull(sqlInsertDefinition.UpsertClause);
        Assert.Equal(SqlUpsertAction.Update, sqlInsertDefinition.UpsertClause.Action);

        Assert.Single(sqlInsertDefinition.UpsertClause.ConflictColumns);
        Assert.Equal("id", sqlInsertDefinition.UpsertClause.ConflictColumns[0].ColumnName);

        Assert.Single(sqlInsertDefinition.UpsertClause.Assignments);

        // WHERE clause on DO UPDATE
        Assert.NotNull(sqlInsertDefinition.UpsertClause.WhereCondition);
        Assert.Null(sqlInsertDefinition.UpsertClause.ConflictTargetWhereCondition);
    }

    [Fact]
    public void Insert_OnConflictWithTargetWhere_DoNothing()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name, status) VALUES (1, 'Alice', 'active') ON CONFLICT (id) WHERE status = 'active' DO NOTHING");

        var sqlInsertDefinition = grammar.Create(node);

        Assert.NotNull(sqlInsertDefinition.UpsertClause);
        Assert.Equal(SqlUpsertAction.DoNothing, sqlInsertDefinition.UpsertClause.Action);

        Assert.Single(sqlInsertDefinition.UpsertClause.ConflictColumns);
        Assert.Equal("id", sqlInsertDefinition.UpsertClause.ConflictColumns[0].ColumnName);

        // WHERE on conflict target (partial index filter)
        Assert.NotNull(sqlInsertDefinition.UpsertClause.ConflictTargetWhereCondition);
        Assert.Null(sqlInsertDefinition.UpsertClause.WhereCondition);
    }

    [Fact]
    public void Insert_OnConflictWithTargetWhere_DoUpdateSet_WithUpdateWhere()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name, status) VALUES (1, 'Alice', 'active') ON CONFLICT (id) WHERE status = 'active' DO UPDATE SET name = EXCLUDED.name WHERE users.id > 0");

        var sqlInsertDefinition = grammar.Create(node);

        Assert.NotNull(sqlInsertDefinition.UpsertClause);
        Assert.Equal(SqlUpsertAction.Update, sqlInsertDefinition.UpsertClause.Action);

        Assert.Single(sqlInsertDefinition.UpsertClause.ConflictColumns);

        // Both WHERE clauses should be present
        Assert.NotNull(sqlInsertDefinition.UpsertClause.ConflictTargetWhereCondition);
        Assert.NotNull(sqlInsertDefinition.UpsertClause.WhereCondition);

        Assert.Single(sqlInsertDefinition.UpsertClause.Assignments);
    }
}
