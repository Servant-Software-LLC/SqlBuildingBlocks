using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.SQLServer.Tests;

public class OutputClauseOptTests
{
    #region Test Grammars

    private class InsertTestGrammar : Grammar
    {
        public InsertTestGrammar() : base(false) // SQL is case insensitive
        {
            SQLServer.SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableHintOpt tableHintOpt = new(this);
            TableName tableName = new(this, aliasOpt, id, tableHintOpt);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);
            OutputClauseOpt outputClauseOpt = new(this, id);
            InsertStmt insertStmt = new(this, id, expr, selectStmt, outputClauseOpt);

            expr.InitializeRule(selectStmt, funcCall);

            Root = insertStmt;
        }

        public SqlInsertDefinition Create(ParseTreeNode insertStmt) =>
            ((InsertStmt)Root).Create(insertStmt);
    }

    private class UpdateTestGrammar : Grammar
    {
        public UpdateTestGrammar() : base(false)
        {
            SQLServer.SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableHintOpt tableHintOpt = new(this);
            TableName tableName = new(this, aliasOpt, id, tableHintOpt);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            ReturningClauseOpt returningClauseOpt = new(this, id);
            OutputClauseOpt outputClauseOpt = new(this, id);
            UpdateStmt updateStmt = new(this, expr, funcCall, tableName, whereClauseOpt, returningClauseOpt, joinChainOpt, outputClauseOpt);

            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = updateStmt;
        }

        public SqlUpdateDefinition Create(ParseTreeNode updateStmt) =>
            ((UpdateStmt)Root).Create(updateStmt);
    }

    private class DeleteTestGrammar : Grammar
    {
        public DeleteTestGrammar() : base(false)
        {
            SQLServer.SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableHintOpt tableHintOpt = new(this);
            TableName tableName = new(this, aliasOpt, id, tableHintOpt);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            ReturningClauseOpt returningClauseOpt = new(this, id);
            OutputClauseOpt outputClauseOpt = new(this, id);
            DeleteStmt deleteStmt = new(this, tableName, whereClauseOpt, returningClauseOpt, joinChainOpt, outputClauseOpt);

            FuncCall funcCall = new(this, id, expr);
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = deleteStmt;
        }

        public SqlDeleteDefinition Create(ParseTreeNode deleteStmt) =>
            ((DeleteStmt)Root).Create(deleteStmt);
    }

    #endregion

    #region INSERT with OUTPUT

    [Fact]
    public void Insert_Output_InsertedStar()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "INSERT INTO [Products] ([Name], [Price]) OUTPUT INSERTED.* VALUES ('Widget', 999)");

        var insertStmt = grammar.Create(node);

        Assert.NotNull(insertStmt.OutputClause);
        Assert.Single(insertStmt.OutputClause!.Columns);
        Assert.Equal("INSERTED", insertStmt.OutputClause.Columns[0].Source);
        Assert.Null(insertStmt.OutputClause.Columns[0].ColumnName); // wildcard
        Assert.Null(insertStmt.OutputClause.IntoTable);

        // Verify the rest of the INSERT parsed correctly
        Assert.Equal("Products", insertStmt.Table!.TableName);
        Assert.Equal(2, insertStmt.Columns.Count);
        Assert.NotNull(insertStmt.Values);
        Assert.Single(insertStmt.Values!);
    }

    [Fact]
    public void Insert_Output_SpecificColumns()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "INSERT INTO [Orders] ([CustomerID], [Amount]) OUTPUT INSERTED.[OrderID], INSERTED.[CustomerID] VALUES (42, 100)");

        var insertStmt = grammar.Create(node);

        Assert.NotNull(insertStmt.OutputClause);
        Assert.Equal(2, insertStmt.OutputClause!.Columns.Count);
        Assert.Equal("INSERTED", insertStmt.OutputClause.Columns[0].Source);
        Assert.Equal("OrderID", insertStmt.OutputClause.Columns[0].ColumnName);
        Assert.Equal("INSERTED", insertStmt.OutputClause.Columns[1].Source);
        Assert.Equal("CustomerID", insertStmt.OutputClause.Columns[1].ColumnName);
    }

    [Fact]
    public void Insert_Output_Into_Table()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "INSERT INTO [Products] ([Name]) OUTPUT INSERTED.[ProductID] INTO [AuditLog] VALUES ('Gadget')");

        var insertStmt = grammar.Create(node);

        Assert.NotNull(insertStmt.OutputClause);
        Assert.Single(insertStmt.OutputClause!.Columns);
        Assert.Equal("INSERTED", insertStmt.OutputClause.Columns[0].Source);
        Assert.Equal("ProductID", insertStmt.OutputClause.Columns[0].ColumnName);
        Assert.NotNull(insertStmt.OutputClause.IntoTable);
        Assert.Equal("AuditLog", insertStmt.OutputClause.IntoTable!.TableName);
    }

    [Fact]
    public void Insert_Without_Output()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "INSERT INTO [Products] ([Name]) VALUES ('Widget')");

        var insertStmt = grammar.Create(node);

        Assert.Null(insertStmt.OutputClause);
        Assert.Equal("Products", insertStmt.Table!.TableName);
    }

    [Fact]
    public void Insert_Output_CaseInsensitive()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "insert into [Products] ([Name]) output inserted.* values ('Widget')");

        var insertStmt = grammar.Create(node);

        Assert.NotNull(insertStmt.OutputClause);
        Assert.Single(insertStmt.OutputClause!.Columns);
        Assert.Equal("INSERTED", insertStmt.OutputClause.Columns[0].Source);
        Assert.Null(insertStmt.OutputClause.Columns[0].ColumnName);
    }

    #endregion

    #region UPDATE with OUTPUT

    [Fact]
    public void Update_Output_InsertedAndDeleted()
    {
        UpdateTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE [Products] SET [Price] = 1999 OUTPUT INSERTED.[Price], DELETED.[Price] WHERE [ProductID] = 1");

        var updateStmt = grammar.Create(node);

        Assert.NotNull(updateStmt.OutputClause);
        Assert.Equal(2, updateStmt.OutputClause!.Columns.Count);
        Assert.Equal("INSERTED", updateStmt.OutputClause.Columns[0].Source);
        Assert.Equal("Price", updateStmt.OutputClause.Columns[0].ColumnName);
        Assert.Equal("DELETED", updateStmt.OutputClause.Columns[1].Source);
        Assert.Equal("Price", updateStmt.OutputClause.Columns[1].ColumnName);

        // Verify rest of UPDATE parsed correctly
        Assert.Equal("Products", updateStmt.Table!.TableName);
        Assert.Single(updateStmt.Assignments);
        Assert.NotNull(updateStmt.WhereClause);
    }

    [Fact]
    public void Update_Output_DeletedStar()
    {
        UpdateTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE [Inventory] SET [Quantity] = 0 OUTPUT DELETED.*");

        var updateStmt = grammar.Create(node);

        Assert.NotNull(updateStmt.OutputClause);
        Assert.Single(updateStmt.OutputClause!.Columns);
        Assert.Equal("DELETED", updateStmt.OutputClause.Columns[0].Source);
        Assert.Null(updateStmt.OutputClause.Columns[0].ColumnName);
    }

    [Fact]
    public void Update_Output_Into_Table()
    {
        UpdateTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE [Products] SET [Price] = 2999 OUTPUT INSERTED.* INTO [PriceHistory] WHERE [ProductID] = 5");

        var updateStmt = grammar.Create(node);

        Assert.NotNull(updateStmt.OutputClause);
        Assert.Single(updateStmt.OutputClause!.Columns);
        Assert.NotNull(updateStmt.OutputClause.IntoTable);
        Assert.Equal("PriceHistory", updateStmt.OutputClause.IntoTable!.TableName);
        Assert.NotNull(updateStmt.WhereClause);
    }

    [Fact]
    public void Update_Without_Output()
    {
        UpdateTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE [Products] SET [Price] = 999 WHERE [ProductID] = 1");

        var updateStmt = grammar.Create(node);

        Assert.Null(updateStmt.OutputClause);
        Assert.Equal("Products", updateStmt.Table!.TableName);
    }

    [Fact]
    public void Update_Output_WithFrom()
    {
        UpdateTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE [p] SET [p].[Price] = 0 OUTPUT DELETED.[ProductID] FROM [Products] [p] WHERE [p].[Discontinued] = 1");

        var updateStmt = grammar.Create(node);

        Assert.NotNull(updateStmt.OutputClause);
        Assert.Single(updateStmt.OutputClause!.Columns);
        Assert.Equal("DELETED", updateStmt.OutputClause.Columns[0].Source);
        Assert.Equal("ProductID", updateStmt.OutputClause.Columns[0].ColumnName);
        Assert.NotNull(updateStmt.SourceTable);
        Assert.Equal("Products", updateStmt.SourceTable!.TableName);
    }

    #endregion

    #region DELETE with OUTPUT

    [Fact]
    public void Delete_Output_DeletedStar()
    {
        DeleteTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "DELETE FROM [Products] OUTPUT DELETED.* WHERE [Discontinued] = 1");

        var deleteStmt = grammar.Create(node);

        Assert.NotNull(deleteStmt.OutputClause);
        Assert.Single(deleteStmt.OutputClause!.Columns);
        Assert.Equal("DELETED", deleteStmt.OutputClause.Columns[0].Source);
        Assert.Null(deleteStmt.OutputClause.Columns[0].ColumnName);
        Assert.Null(deleteStmt.OutputClause.IntoTable);
        Assert.NotNull(deleteStmt.WhereClause);
    }

    [Fact]
    public void Delete_Output_SpecificColumns()
    {
        DeleteTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "DELETE FROM [Orders] OUTPUT DELETED.[OrderID], DELETED.[CustomerID] WHERE [Status] = 'Cancelled'");

        var deleteStmt = grammar.Create(node);

        Assert.NotNull(deleteStmt.OutputClause);
        Assert.Equal(2, deleteStmt.OutputClause!.Columns.Count);
        Assert.Equal("DELETED", deleteStmt.OutputClause.Columns[0].Source);
        Assert.Equal("OrderID", deleteStmt.OutputClause.Columns[0].ColumnName);
        Assert.Equal("DELETED", deleteStmt.OutputClause.Columns[1].Source);
        Assert.Equal("CustomerID", deleteStmt.OutputClause.Columns[1].ColumnName);
    }

    [Fact]
    public void Delete_Output_Into_Table()
    {
        DeleteTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "DELETE FROM [Products] OUTPUT DELETED.* INTO [DeletedProducts] WHERE [ProductID] = 99");

        var deleteStmt = grammar.Create(node);

        Assert.NotNull(deleteStmt.OutputClause);
        Assert.NotNull(deleteStmt.OutputClause!.IntoTable);
        Assert.Equal("DeletedProducts", deleteStmt.OutputClause.IntoTable!.TableName);
    }

    [Fact]
    public void Delete_Without_Output()
    {
        DeleteTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "DELETE FROM [Products] WHERE [ProductID] = 1");

        var deleteStmt = grammar.Create(node);

        Assert.Null(deleteStmt.OutputClause);
        Assert.Equal("Products", deleteStmt.Table!.TableName);
    }

    [Fact]
    public void Delete_Output_WithTarget()
    {
        DeleteTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "DELETE [o] FROM [Orders] [o] OUTPUT DELETED.[OrderID] WHERE [o].[Status] = 'Old'");

        var deleteStmt = grammar.Create(node);

        Assert.NotNull(deleteStmt.OutputClause);
        Assert.Single(deleteStmt.OutputClause!.Columns);
        Assert.Equal("DELETED", deleteStmt.OutputClause.Columns[0].Source);
        Assert.Equal("OrderID", deleteStmt.OutputClause.Columns[0].ColumnName);
        Assert.NotNull(deleteStmt.Table);
        Assert.Equal("o", deleteStmt.Table!.TableName);
        Assert.NotNull(deleteStmt.SourceTable);
        Assert.Equal("Orders", deleteStmt.SourceTable!.TableName);
    }

    #endregion

    #region OUTPUT INTO with schema-qualified tables

    [Fact]
    public void Insert_Output_Into_SchemaQualifiedTable()
    {
        InsertTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "INSERT INTO [Products] ([Name]) OUTPUT INSERTED.[ProductID] INTO dbo.[AuditLog] VALUES ('Item')");

        var insertStmt = grammar.Create(node);

        Assert.NotNull(insertStmt.OutputClause);
        Assert.NotNull(insertStmt.OutputClause!.IntoTable);
        Assert.Equal("AuditLog", insertStmt.OutputClause.IntoTable!.TableName);
        Assert.Equal("dbo", insertStmt.OutputClause.IntoTable.DatabaseName);
    }

    #endregion
}
