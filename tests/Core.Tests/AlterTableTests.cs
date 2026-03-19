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
        Assert.Empty(result.ConstraintsToDrop);
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
        Assert.Empty(result.ConstraintsToDrop);
        Assert.Single(result.ConstraintsToAdd);

        var constraint = result.ConstraintsToAdd[0];
        Assert.Equal("", constraint.Name);
        Assert.NotNull(constraint.CheckConstraint);
    }

    [Fact]
    public void AddConstraint_PrimaryKey_Named()
    {
        const string sql = "ALTER TABLE employees ADD CONSTRAINT pk_employees PRIMARY KEY (employee_id)";

        var grammar = new CheckTestGrammar();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal("employees", result.Table!.TableName);
        Assert.Empty(result.ColumnsToAdd);
        Assert.Empty(result.ColumnsToDrop);
        Assert.Empty(result.ConstraintsToDrop);
        Assert.Single(result.ConstraintsToAdd);

        var constraint = result.ConstraintsToAdd[0];
        Assert.Equal("pk_employees", constraint.Name);
        Assert.NotNull(constraint.PrimaryKeyConstraint);
        Assert.Equal(new[] { "employee_id" }, constraint.PrimaryKeyConstraint!.Columns);
    }

    [Fact]
    public void AddConstraint_Unique_Named()
    {
        const string sql = "ALTER TABLE employees ADD CONSTRAINT uq_employees_email UNIQUE (email)";

        var grammar = new CheckTestGrammar();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal("employees", result.Table!.TableName);
        Assert.Empty(result.ColumnsToAdd);
        Assert.Empty(result.ColumnsToDrop);
        Assert.Empty(result.ConstraintsToDrop);
        Assert.Single(result.ConstraintsToAdd);

        var constraint = result.ConstraintsToAdd[0];
        Assert.Equal("uq_employees_email", constraint.Name);
        Assert.NotNull(constraint.UniqueConstraint);
        Assert.Equal(new[] { "email" }, constraint.UniqueConstraint!.Columns);
    }

    [Fact]
    public void AddConstraint_ForeignKey_Named()
    {
        const string sql = "ALTER TABLE orders ADD CONSTRAINT fk_orders_customer FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE";

        var grammar = new CheckTestGrammar();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal("orders", result.Table!.TableName);
        Assert.Empty(result.ColumnsToAdd);
        Assert.Empty(result.ColumnsToDrop);
        Assert.Empty(result.ConstraintsToDrop);
        Assert.Single(result.ConstraintsToAdd);

        var constraint = result.ConstraintsToAdd[0];
        Assert.Equal("fk_orders_customer", constraint.Name);
        Assert.NotNull(constraint.ForeignKeyConstraint);
        Assert.Equal("customers", constraint.ForeignKeyConstraint!.ParentTable.TableName);
        Assert.Equal(ForeignKeyReferentialAction.Cascade, constraint.ForeignKeyConstraint.OnDeleteAction);
        Assert.Single(constraint.ForeignKeyConstraint.ColumnReferences);
        Assert.Equal(("customer_id", "id"), constraint.ForeignKeyConstraint.ColumnReferences[0]);
    }

    [Fact]
    public void DropConstraint_Named()
    {
        const string sql = "ALTER TABLE employees DROP CONSTRAINT chk_age";

        var grammar = new CheckTestGrammar();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal("employees", result.Table!.TableName);
        Assert.Empty(result.ColumnsToAdd);
        Assert.Empty(result.ColumnsToDrop);
        Assert.Empty(result.ConstraintsToAdd);
        Assert.Single(result.ConstraintsToDrop);
        Assert.Equal("chk_age", result.ConstraintsToDrop[0]);
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

    [Fact]
    public void AlterColumn_Type_Ansi()
    {
        const string sql = @"ALTER TABLE products ALTER COLUMN price TYPE DECIMAL(12,4)";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal("products", result.Table!.TableName);
        Assert.Empty(result.ColumnsToAdd);
        Assert.Empty(result.ColumnsToDrop);
        Assert.Empty(result.ColumnsToRename);
        Assert.Single(result.ColumnsToAlter);

        var action = result.ColumnsToAlter[0];
        Assert.Equal(SqlAlterColumnOperation.Alter, action.Operation);
        Assert.Equal("price", action.SourceColumnName);
        Assert.Equal("price", action.Column.ColumnName);
        Assert.Equal("DECIMAL", action.Column.DataType.Name);
        Assert.Equal(12, action.Column.DataType.Precision);
        Assert.Equal(4, action.Column.DataType.Scale);
    }

    [Fact]
    public void ModifyColumn_MySql()
    {
        const string sql = @"ALTER TABLE products MODIFY COLUMN price DECIMAL(12,4) NOT NULL";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal("products", result.Table!.TableName);
        Assert.Single(result.ColumnsToAlter);

        var action = result.ColumnsToAlter[0];
        Assert.Equal(SqlAlterColumnOperation.Modify, action.Operation);
        Assert.Equal("price", action.SourceColumnName);
        Assert.Equal("price", action.Column.ColumnName);
        Assert.Equal("DECIMAL", action.Column.DataType.Name);
        Assert.Equal(12, action.Column.DataType.Precision);
        Assert.Equal(4, action.Column.DataType.Scale);
        Assert.False(action.Column.AllowNulls);
    }

    [Fact]
    public void ChangeColumn_MySql()
    {
        const string sql = @"ALTER TABLE products CHANGE COLUMN old_name new_name VARCHAR(100)";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal("products", result.Table!.TableName);
        Assert.Single(result.ColumnsToAlter);

        var action = result.ColumnsToAlter[0];
        Assert.Equal(SqlAlterColumnOperation.Change, action.Operation);
        Assert.Equal("old_name", action.SourceColumnName);
        Assert.Equal("new_name", action.Column.ColumnName);
        Assert.Equal("VARCHAR", action.Column.DataType.Name);
        Assert.Equal(100, action.Column.DataType.Length);
    }

    [Fact]
    public void AlterColumn_SqlServer()
    {
        const string sql = @"ALTER TABLE products ALTER COLUMN price DECIMAL(12,4) NOT NULL";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal("products", result.Table!.TableName);
        Assert.Single(result.ColumnsToAlter);

        var action = result.ColumnsToAlter[0];
        Assert.Equal(SqlAlterColumnOperation.Alter, action.Operation);
        Assert.Equal("price", action.SourceColumnName);
        Assert.Equal("price", action.Column.ColumnName);
        Assert.Equal("DECIMAL", action.Column.DataType.Name);
        Assert.Equal(12, action.Column.DataType.Precision);
        Assert.Equal(4, action.Column.DataType.Scale);
        Assert.False(action.Column.AllowNulls);
    }

}
