using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.PostgreSQL.Tests;

public class ReturningClauseTests
{
    #region Test Grammars

    private class InsertTestGrammar : Grammar
    {
        public InsertTestGrammar() : base(false) // SQL is case insensitive
        {
            SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            PostgreSQL.Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            PostgreSQL.ReturningClauseOpt returningClauseOpt = new(this, expr, aliasOpt);
            PostgreSQL.InsertStmt insertStmt = new(this, id, expr, selectStmt, returningClauseOpt);

            Root = insertStmt;
        }

        public SqlInsertDefinition Create(ParseTreeNode insertStmt) =>
            ((PostgreSQL.InsertStmt)Root).Create(insertStmt);
    }

    private class UpdateTestGrammar : Grammar
    {
        public UpdateTestGrammar() : base(false)
        {
            SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            PostgreSQL.Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            SqlBuildingBlocks.ReturningClauseOpt baseReturningClauseOpt = new(this, id);
            PostgreSQL.ReturningClauseOpt pgReturningClauseOpt = new(this, expr, aliasOpt);
            PostgreSQL.UpdateStmt updateStmt = new(this, expr, funcCall, tableName, whereClauseOpt,
                baseReturningClauseOpt, joinChainOpt, pgReturningClauseOpt);

            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = updateStmt;
        }

        public SqlUpdateDefinition Create(ParseTreeNode updateStmt) =>
            ((PostgreSQL.UpdateStmt)Root).Create(updateStmt);
    }

    private class DeleteTestGrammar : Grammar
    {
        public DeleteTestGrammar() : base(false)
        {
            SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableName tableName = new(this, aliasOpt, id);
            Parameter parameter = new(this);
            PostgreSQL.Expr expr = new(this, id, literalValue, parameter);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            SqlBuildingBlocks.ReturningClauseOpt baseReturningClauseOpt = new(this, id);
            PostgreSQL.ReturningClauseOpt pgReturningClauseOpt = new(this, expr, aliasOpt);
            PostgreSQL.DeleteStmt deleteStmt = new(this, tableName, whereClauseOpt,
                baseReturningClauseOpt, joinChainOpt, pgReturningClauseOpt);

            FuncCall funcCall = new(this, id, expr);
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = deleteStmt;
        }

        public SqlDeleteDefinition Create(ParseTreeNode deleteStmt) =>
            ((PostgreSQL.DeleteStmt)Root).Create(deleteStmt);
    }

    #endregion

    #region INSERT with RETURNING

    [Fact]
    public void Insert_Returning_Star()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name) VALUES (1, 'Alice') RETURNING *");

        var insertDef = grammar.Create(node);

        Assert.NotNull(insertDef.ReturningClause);
        Assert.Single(insertDef.ReturningClause!.Items);
        Assert.True(insertDef.ReturningClause.Items[0].IsWildcard);

        // Verify the rest of the INSERT parsed correctly
        Assert.Equal("users", insertDef.Table!.TableName);
        Assert.Equal(2, insertDef.Columns.Count);
        Assert.NotNull(insertDef.Values);
        Assert.Single(insertDef.Values!);
    }

    [Fact]
    public void Insert_Returning_SingleColumn()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (name, email) VALUES ('Alice', 'alice@example.com') RETURNING id");

        var insertDef = grammar.Create(node);

        Assert.NotNull(insertDef.ReturningClause);
        Assert.Single(insertDef.ReturningClause!.Items);
        var item = insertDef.ReturningClause.Items[0];
        Assert.False(item.IsWildcard);
        Assert.NotNull(item.Expression);
        Assert.NotNull(item.Expression!.Column);
        Assert.Equal("id", item.Expression.Column!.ColumnName);
        Assert.Null(item.Alias);
    }

    [Fact]
    public void Insert_Returning_MultipleColumns()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (name, email) VALUES ('Alice', 'alice@example.com') RETURNING id, name, email");

        var insertDef = grammar.Create(node);

        Assert.NotNull(insertDef.ReturningClause);
        Assert.Equal(3, insertDef.ReturningClause!.Items.Count);

        Assert.Equal("id", insertDef.ReturningClause.Items[0].Expression!.Column!.ColumnName);
        Assert.Equal("name", insertDef.ReturningClause.Items[1].Expression!.Column!.ColumnName);
        Assert.Equal("email", insertDef.ReturningClause.Items[2].Expression!.Column!.ColumnName);
    }

    [Fact]
    public void Insert_Returning_WithAlias()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (name) VALUES ('Alice') RETURNING id AS user_id");

        var insertDef = grammar.Create(node);

        Assert.NotNull(insertDef.ReturningClause);
        Assert.Single(insertDef.ReturningClause!.Items);
        var item = insertDef.ReturningClause.Items[0];
        Assert.False(item.IsWildcard);
        Assert.NotNull(item.Expression);
        Assert.Equal("id", item.Expression!.Column!.ColumnName);
        Assert.Equal("user_id", item.Alias);
    }

    [Fact]
    public void Insert_Without_Returning()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name) VALUES (1, 'Alice')");

        var insertDef = grammar.Create(node);

        Assert.Null(insertDef.ReturningClause);
        Assert.Equal("users", insertDef.Table!.TableName);
    }

    [Fact]
    public void Insert_OnConflict_With_Returning()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name) VALUES (1, 'Alice') ON CONFLICT (id) DO NOTHING RETURNING *");

        var insertDef = grammar.Create(node);

        // Verify RETURNING
        Assert.NotNull(insertDef.ReturningClause);
        Assert.Single(insertDef.ReturningClause!.Items);
        Assert.True(insertDef.ReturningClause.Items[0].IsWildcard);

        // Verify ON CONFLICT
        Assert.NotNull(insertDef.UpsertClause);
        Assert.Equal(SqlUpsertAction.DoNothing, insertDef.UpsertClause.Action);
        Assert.Single(insertDef.UpsertClause.ConflictColumns);
        Assert.Equal("id", insertDef.UpsertClause.ConflictColumns[0].ColumnName);
    }

    [Fact]
    public void Insert_OnConflictDoUpdate_With_Returning_MultipleColumns()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (id, name, email) VALUES (1, 'Alice', 'alice@example.com') ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name RETURNING id, name");

        var insertDef = grammar.Create(node);

        // Verify RETURNING
        Assert.NotNull(insertDef.ReturningClause);
        Assert.Equal(2, insertDef.ReturningClause!.Items.Count);
        Assert.Equal("id", insertDef.ReturningClause.Items[0].Expression!.Column!.ColumnName);
        Assert.Equal("name", insertDef.ReturningClause.Items[1].Expression!.Column!.ColumnName);

        // Verify ON CONFLICT
        Assert.NotNull(insertDef.UpsertClause);
        Assert.Equal(SqlUpsertAction.Update, insertDef.UpsertClause.Action);
    }

    [Fact]
    public void Insert_Returning_CaseInsensitive()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "insert into users (id, name) values (1, 'Alice') returning *");

        var insertDef = grammar.Create(node);

        Assert.NotNull(insertDef.ReturningClause);
        Assert.Single(insertDef.ReturningClause!.Items);
        Assert.True(insertDef.ReturningClause.Items[0].IsWildcard);
    }

    #endregion

    #region UPDATE with RETURNING

    [Fact]
    public void Update_Returning_Star()
    {
        UpdateTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "UPDATE users SET name = 'Bob' RETURNING *");

        var updateDef = grammar.Create(node);

        Assert.NotNull(updateDef.ReturningClause);
        Assert.Single(updateDef.ReturningClause!.Items);
        Assert.True(updateDef.ReturningClause.Items[0].IsWildcard);

        // Verify rest of UPDATE parsed correctly
        Assert.Equal("users", updateDef.Table!.TableName);
        Assert.Single(updateDef.Assignments);
    }

    [Fact]
    public void Update_Returning_MultipleColumns()
    {
        UpdateTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "UPDATE users SET name = 'Bob' WHERE id = 1 RETURNING id, name, email");

        var updateDef = grammar.Create(node);

        Assert.NotNull(updateDef.ReturningClause);
        Assert.Equal(3, updateDef.ReturningClause!.Items.Count);
        Assert.Equal("id", updateDef.ReturningClause.Items[0].Expression!.Column!.ColumnName);
        Assert.Equal("name", updateDef.ReturningClause.Items[1].Expression!.Column!.ColumnName);
        Assert.Equal("email", updateDef.ReturningClause.Items[2].Expression!.Column!.ColumnName);
        Assert.NotNull(updateDef.WhereClause);
    }

    [Fact]
    public void Update_Returning_WithAlias()
    {
        UpdateTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "UPDATE products SET price = 999 RETURNING id AS product_id, price AS new_price");

        var updateDef = grammar.Create(node);

        Assert.NotNull(updateDef.ReturningClause);
        Assert.Equal(2, updateDef.ReturningClause!.Items.Count);

        Assert.Equal("id", updateDef.ReturningClause.Items[0].Expression!.Column!.ColumnName);
        Assert.Equal("product_id", updateDef.ReturningClause.Items[0].Alias);

        Assert.Equal("price", updateDef.ReturningClause.Items[1].Expression!.Column!.ColumnName);
        Assert.Equal("new_price", updateDef.ReturningClause.Items[1].Alias);
    }

    [Fact]
    public void Update_Without_Returning()
    {
        UpdateTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "UPDATE users SET name = 'Bob' WHERE id = 1");

        var updateDef = grammar.Create(node);

        Assert.Null(updateDef.ReturningClause);
        Assert.Equal("users", updateDef.Table!.TableName);
    }

    #endregion

    #region DELETE with RETURNING

    [Fact]
    public void Delete_Returning_Star()
    {
        DeleteTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "DELETE FROM users WHERE id = 1 RETURNING *");

        var deleteDef = grammar.Create(node);

        Assert.NotNull(deleteDef.ReturningClause);
        Assert.Single(deleteDef.ReturningClause!.Items);
        Assert.True(deleteDef.ReturningClause.Items[0].IsWildcard);

        Assert.NotNull(deleteDef.WhereClause);
        Assert.Equal("users", deleteDef.Table!.TableName);
    }

    [Fact]
    public void Delete_Returning_MultipleColumns()
    {
        DeleteTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "DELETE FROM orders WHERE status = 'cancelled' RETURNING id, customer_id, amount");

        var deleteDef = grammar.Create(node);

        Assert.NotNull(deleteDef.ReturningClause);
        Assert.Equal(3, deleteDef.ReturningClause!.Items.Count);
        Assert.Equal("id", deleteDef.ReturningClause.Items[0].Expression!.Column!.ColumnName);
        Assert.Equal("customer_id", deleteDef.ReturningClause.Items[1].Expression!.Column!.ColumnName);
        Assert.Equal("amount", deleteDef.ReturningClause.Items[2].Expression!.Column!.ColumnName);
    }

    [Fact]
    public void Delete_Returning_WithAlias()
    {
        DeleteTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "DELETE FROM users WHERE id = 1 RETURNING id AS deleted_id, name AS deleted_name");

        var deleteDef = grammar.Create(node);

        Assert.NotNull(deleteDef.ReturningClause);
        Assert.Equal(2, deleteDef.ReturningClause!.Items.Count);

        Assert.Equal("id", deleteDef.ReturningClause.Items[0].Expression!.Column!.ColumnName);
        Assert.Equal("deleted_id", deleteDef.ReturningClause.Items[0].Alias);

        Assert.Equal("name", deleteDef.ReturningClause.Items[1].Expression!.Column!.ColumnName);
        Assert.Equal("deleted_name", deleteDef.ReturningClause.Items[1].Alias);
    }

    [Fact]
    public void Delete_Without_Returning()
    {
        DeleteTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "DELETE FROM users WHERE id = 1");

        var deleteDef = grammar.Create(node);

        Assert.Null(deleteDef.ReturningClause);
        Assert.Equal("users", deleteDef.Table!.TableName);
    }

    [Fact]
    public void Delete_Returning_SingleColumn()
    {
        DeleteTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "DELETE FROM users RETURNING id");

        var deleteDef = grammar.Create(node);

        Assert.NotNull(deleteDef.ReturningClause);
        Assert.Single(deleteDef.ReturningClause!.Items);
        var item = deleteDef.ReturningClause.Items[0];
        Assert.False(item.IsWildcard);
        Assert.Equal("id", item.Expression!.Column!.ColumnName);
    }

    #endregion

    #region RETURNING with expressions

    [Fact]
    public void Insert_Returning_QualifiedColumn()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "INSERT INTO users (name) VALUES ('Alice') RETURNING users.id, users.name");

        var insertDef = grammar.Create(node);

        Assert.NotNull(insertDef.ReturningClause);
        Assert.Equal(2, insertDef.ReturningClause!.Items.Count);

        var item1 = insertDef.ReturningClause.Items[0];
        Assert.Equal("users", item1.Expression!.Column!.TableName);
        Assert.Equal("id", item1.Expression.Column.ColumnName);

        var item2 = insertDef.ReturningClause.Items[1];
        Assert.Equal("users", item2.Expression!.Column!.TableName);
        Assert.Equal("name", item2.Expression.Column.ColumnName);
    }

    #endregion
}
