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

}
