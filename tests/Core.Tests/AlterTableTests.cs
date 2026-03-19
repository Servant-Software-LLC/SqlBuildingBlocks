using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class AlterTableTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            Id id = new(this, simpleId);
            DataType dataType = new(this);

            AlterStmt alterStmt = new(this, id, dataType);

            Root = alterStmt;
        }

        public virtual SqlAlterTableDefinition Create(ParseTreeNode createTableStmt) =>
            ((AlterStmt)Root).Create(createTableStmt);

    }

    [Fact]
    public void AddColumn_Simple()
    {
        //Setup
        const string sql = @"ALTER TABLE Customers ADD Age INT";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var sqlAlterTableDefinition = grammar.Create(node);

        //Assert
        Assert.Equal("Customers", sqlAlterTableDefinition.Table.TableName);
        Assert.Empty(sqlAlterTableDefinition.ColumnsToDrop);
        Assert.Single(sqlAlterTableDefinition.ColumnsToAdd);
        var columnToAdd = sqlAlterTableDefinition.ColumnsToAdd[0];
        Assert.Equal("Age", columnToAdd.Column.ColumnName);
        Assert.Equal("INT", columnToAdd.Column.DataType.Name);
        Assert.True(columnToAdd.Column.AllowNulls);
    }

    [Fact]
    public void AddColumn_Simple_WithColumnKeyword()
    {
        //Setup
        const string sql = @"ALTER TABLE Customers ADD COLUMN Age INT NULL";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var sqlAlterTableDefinition = grammar.Create(node);

        //Assert
        Assert.Equal("Customers", sqlAlterTableDefinition.Table.TableName);
        Assert.Empty(sqlAlterTableDefinition.ColumnsToDrop);
        Assert.Single(sqlAlterTableDefinition.ColumnsToAdd);
        var columnToAdd = sqlAlterTableDefinition.ColumnsToAdd[0];
        Assert.Equal("Age", columnToAdd.Column.ColumnName);
        Assert.Equal("INT", columnToAdd.Column.DataType.Name);
        Assert.True(columnToAdd.Column.AllowNulls);
    }

    [Fact]
    public void AddColumn_NotNull()
    {
        //Setup
        const string sql = @"ALTER TABLE Customers ADD Age INT NOT NULL";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var sqlAlterTableDefinition = grammar.Create(node);

        //Assert
        Assert.Equal("Customers", sqlAlterTableDefinition.Table.TableName);
        Assert.Empty(sqlAlterTableDefinition.ColumnsToDrop);
        Assert.Single(sqlAlterTableDefinition.ColumnsToAdd);
        var columnToAdd = sqlAlterTableDefinition.ColumnsToAdd[0];
        Assert.Equal("Age", columnToAdd.Column.ColumnName);
        Assert.Equal("INT", columnToAdd.Column.DataType.Name);
        Assert.False(columnToAdd.Column.AllowNulls);
    }

    [Fact]
    public void AddColumn_Unique()
    {
        //Setup
        const string sql = @"ALTER TABLE Customers ADD Age INT UNIQUE";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var sqlAlterTableDefinition = grammar.Create(node);

        //Assert
        Assert.Equal("Customers", sqlAlterTableDefinition.Table.TableName);
        Assert.Empty(sqlAlterTableDefinition.ColumnsToDrop);
        Assert.Single(sqlAlterTableDefinition.ColumnsToAdd);
        var columnToAdd = sqlAlterTableDefinition.ColumnsToAdd[0];
        Assert.Equal("Age", columnToAdd.Column.ColumnName);
        Assert.Equal("INT", columnToAdd.Column.DataType.Name);
        Assert.True(columnToAdd.Column.AllowNulls);
        Assert.Single(columnToAdd.Constraints);
        var constraint = columnToAdd.Constraints[0];
        Assert.NotNull(constraint.UniqueConstraint);
        Assert.Null(constraint.ForeignKeyConstraint);
        Assert.Null(constraint.PrimaryKeyConstraint);
    }

    [Fact]
    public void DropColumn_Simple()
    {
        //Setup
        const string sql = @"ALTER TABLE Customers DROP Age";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var sqlAlterTableDefinition = grammar.Create(node);

        //Assert
        Assert.Equal("Customers", sqlAlterTableDefinition.Table.TableName);
        Assert.Empty(sqlAlterTableDefinition.ColumnsToAdd);
        Assert.Single(sqlAlterTableDefinition.ColumnsToDrop);
        var columnToDrop = sqlAlterTableDefinition.ColumnsToDrop[0];
        Assert.Equal("Age", columnToDrop);
    }

    [Fact]
    public void DropColumn_Simple_WithColumnKeyword()
    {
        //Setup
        const string sql = @"ALTER TABLE Customers DROP COLUMN Age";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var sqlAlterTableDefinition = grammar.Create(node);

        //Assert
        Assert.Equal("Customers", sqlAlterTableDefinition.Table.TableName);
        Assert.Empty(sqlAlterTableDefinition.ColumnsToAdd);
        Assert.Single(sqlAlterTableDefinition.ColumnsToDrop);
        var columnToDrop = sqlAlterTableDefinition.ColumnsToDrop[0];
        Assert.Equal("Age", columnToDrop);
    }

    private class CheckTestGrammar : Grammar
    {
        public CheckTestGrammar()
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

            DataType dataType = new(this);
            AlterStmt alterStmt = new(this, id, dataType, expr);

            Root = alterStmt;
        }

        public SqlAlterTableDefinition Create(ParseTreeNode node) =>
            ((AlterStmt)Root).Create(node);
    }

    [Fact]
    public void AddConstraint_Check_Named()
    {
        const string sql = "ALTER TABLE employees ADD CONSTRAINT chk_age CHECK (age >= 18)";

        var grammar = new CheckTestGrammar();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal("employees", result.Table!.TableName);
        Assert.Empty(result.ColumnsToAdd);
        Assert.Empty(result.ColumnsToDrop);
        Assert.Single(result.ConstraintsToAdd);

        var constraint = result.ConstraintsToAdd[0];
        Assert.Equal("chk_age", constraint.Name);
        Assert.NotNull(constraint.CheckConstraint);
        Assert.NotNull(constraint.CheckConstraint!.Expression.BinExpr);
    }

    [Fact]
    public void AddConstraint_Check_Unnamed()
    {
        const string sql = "ALTER TABLE employees ADD CHECK (salary > 0)";

        var grammar = new CheckTestGrammar();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal("employees", result.Table!.TableName);
        Assert.Empty(result.ColumnsToAdd);
        Assert.Empty(result.ColumnsToDrop);
        Assert.Single(result.ConstraintsToAdd);

        var constraint = result.ConstraintsToAdd[0];
        Assert.Equal("", constraint.Name);
        Assert.NotNull(constraint.CheckConstraint);
    }

    [Fact]
    public void RenameColumn_Simple()
    {
        //Setup
        const string sql = @"ALTER TABLE Customers RENAME Age TO BirthYear";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var sqlAlterTableDefinition = grammar.Create(node);

        //Assert
        Assert.Equal("Customers", sqlAlterTableDefinition.Table!.TableName);
        Assert.Empty(sqlAlterTableDefinition.ColumnsToAdd);
        Assert.Empty(sqlAlterTableDefinition.ColumnsToDrop);
        Assert.Single(sqlAlterTableDefinition.ColumnsToRename);
        var (oldName, newName) = sqlAlterTableDefinition.ColumnsToRename[0];
        Assert.Equal("Age", oldName);
        Assert.Equal("BirthYear", newName);
    }

    [Fact]
    public void RenameColumn_WithColumnKeyword()
    {
        //Setup
        const string sql = @"ALTER TABLE Customers RENAME COLUMN Age TO BirthYear";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var sqlAlterTableDefinition = grammar.Create(node);

        //Assert
        Assert.Equal("Customers", sqlAlterTableDefinition.Table!.TableName);
        Assert.Empty(sqlAlterTableDefinition.ColumnsToAdd);
        Assert.Empty(sqlAlterTableDefinition.ColumnsToDrop);
        Assert.Single(sqlAlterTableDefinition.ColumnsToRename);
        var (oldName, newName) = sqlAlterTableDefinition.ColumnsToRename[0];
        Assert.Equal("Age", oldName);
        Assert.Equal("BirthYear", newName);
    }

}
