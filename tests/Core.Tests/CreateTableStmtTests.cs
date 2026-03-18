using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class CreateTableStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            Id id = new(this, simpleId);
            DataType dataType = new(this);

            CreateTableStmt createTableStmt = new(this, id, dataType);

            Root = createTableStmt;
        }

        public virtual SqlCreateTableDefinition Create(ParseTreeNode createTableStmt) =>
            ((CreateTableStmt)Root).Create(createTableStmt);

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
            CreateTableStmt createTableStmt = new(this, id, dataType, expr);

            Root = createTableStmt;
        }

        public SqlCreateTableDefinition Create(ParseTreeNode node) =>
            ((CreateTableStmt)Root).Create(node);
    }



    [Fact]
    public void DefaultLiteralValue()
    {
        //Setup
        const string sql = @"
CREATE TABLE [Audit] (
    [Id] INTEGER,
    [Status] VARCHAR(50) DEFAULT 'active',
    [Count] INTEGER DEFAULT 0,
    [Active] BOOLEAN DEFAULT TRUE
)
";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var sqlCreateTableDefinition = grammar.Create(node);

        //Assert
        Assert.Equal(4, sqlCreateTableDefinition.Columns.Count);

        Assert.Null(sqlCreateTableDefinition.Columns[0].DefaultLiteralValue);
        Assert.Null(sqlCreateTableDefinition.Columns[0].DefaultFunctionValue);

        Assert.Equal("active", sqlCreateTableDefinition.Columns[1].DefaultLiteralValue!.String);
        Assert.Null(sqlCreateTableDefinition.Columns[1].DefaultFunctionValue);

        Assert.Equal(0, sqlCreateTableDefinition.Columns[2].DefaultLiteralValue!.Int);
        Assert.Null(sqlCreateTableDefinition.Columns[2].DefaultFunctionValue);

        Assert.Equal(true, sqlCreateTableDefinition.Columns[3].DefaultLiteralValue!.Boolean);
        Assert.Null(sqlCreateTableDefinition.Columns[3].DefaultFunctionValue);
    }

    [Fact]
    public void DefaultFuncCall()
    {
        //Setup
        const string sql = @"
CREATE TABLE [Log] (
    [Id] INTEGER,
    [CreatedAt] TIMESTAMP DEFAULT GETDATE()
)
";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var sqlCreateTableDefinition = grammar.Create(node);

        //Assert
        Assert.Equal(2, sqlCreateTableDefinition.Columns.Count);

        Assert.Null(sqlCreateTableDefinition.Columns[0].DefaultLiteralValue);
        Assert.Null(sqlCreateTableDefinition.Columns[0].DefaultFunctionValue);

        Assert.Null(sqlCreateTableDefinition.Columns[1].DefaultLiteralValue);
        Assert.Equal("GETDATE", sqlCreateTableDefinition.Columns[1].DefaultFunctionValue!.FunctionName);
    }

    [Fact]
    public void BasicTable()
    {
        //Setup
        const string sql = @"
CREATE TABLE [SomeSetting] (
    [Id] INTEGER,
    [SomeProperty] VARCHAR(255),
    CONSTRAINT [PK_SomeSetting] PRIMARY KEY (Id)
)
";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var sqlCreateTableDefinition = grammar.Create(node);

        //Assert
        Assert.Equal(2, sqlCreateTableDefinition.Columns.Count);

        //Id column
        Assert.Equal("Id", sqlCreateTableDefinition.Columns[0].ColumnName);
        Assert.Equal("INTEGER", sqlCreateTableDefinition.Columns[0].DataType.Name);
        //Note: PRIMARY KEY constraints implicitly make a column NOT NULL
        Assert.False(sqlCreateTableDefinition.Columns[0].AllowNulls);
        Assert.False(sqlCreateTableDefinition.Columns[0].DataType.Length.HasValue);
        Assert.False(sqlCreateTableDefinition.Columns[0].DataType.Precision.HasValue);
        Assert.False(sqlCreateTableDefinition.Columns[0].DataType.Scale.HasValue);

        //SomeProperty column
        Assert.Equal("SomeProperty", sqlCreateTableDefinition.Columns[1].ColumnName);
        Assert.Equal("VARCHAR", sqlCreateTableDefinition.Columns[1].DataType.Name);
        Assert.True(sqlCreateTableDefinition.Columns[1].AllowNulls);
        Assert.Equal(255, sqlCreateTableDefinition.Columns[1].DataType.Length.Value);
        Assert.False(sqlCreateTableDefinition.Columns[1].DataType.Precision.HasValue);
        Assert.False(sqlCreateTableDefinition.Columns[1].DataType.Scale.HasValue);


        //Constraints
        Assert.Single(sqlCreateTableDefinition.Constraints);
        var constraint = sqlCreateTableDefinition.Constraints[0];
        Assert.Equal("PK_SomeSetting", constraint.Name);
        Assert.Null(constraint.UniqueConstraint);
        Assert.Null(constraint.ForeignKeyConstraint);
        var primaryKeyConstraint = constraint.PrimaryKeyConstraint;
        Assert.Single(primaryKeyConstraint.Columns);
        Assert.Equal("Id", primaryKeyConstraint.Columns[0]);
    }

    [Fact]
    public void Check_NamedTableConstraint()
    {
        const string sql = @"
CREATE TABLE employees (
    age INTEGER,
    salary INTEGER,
    CONSTRAINT chk_age CHECK (age >= 18),
    CONSTRAINT chk_salary CHECK (salary > 0)
)
";
        var grammar = new CheckTestGrammar();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal(2, result.Columns.Count);
        Assert.Equal(2, result.Constraints.Count);

        var chkAge = result.Constraints[0];
        Assert.Equal("chk_age", chkAge.Name);
        Assert.NotNull(chkAge.CheckConstraint);
        Assert.NotNull(chkAge.CheckConstraint!.Expression.BinExpr);

        var chkSalary = result.Constraints[1];
        Assert.Equal("chk_salary", chkSalary.Name);
        Assert.NotNull(chkSalary.CheckConstraint);
    }

    [Fact]
    public void Check_UnnamedTableConstraint()
    {
        const string sql = @"
CREATE TABLE products (
    price INTEGER,
    CHECK (price > 0)
)
";
        var grammar = new CheckTestGrammar();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Single(result.Columns);
        Assert.Single(result.Constraints);

        var check = result.Constraints[0];
        Assert.Equal("", check.Name);
        Assert.NotNull(check.CheckConstraint);
    }

    [Fact]
    public void Check_InlineColumnConstraint()
    {
        const string sql = @"
CREATE TABLE orders (
    quantity INTEGER CHECK (quantity > 0)
)
";
        var grammar = new CheckTestGrammar();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Single(result.Columns);
        Assert.Single(result.Constraints);

        var check = result.Constraints[0];
        Assert.Equal("CK_quantity", check.Name);
        Assert.NotNull(check.CheckConstraint);
        Assert.NotNull(check.CheckConstraint!.Expression.BinExpr);
    }

    [Fact]
    public void AutoIncrement_MySQL()
    {
        const string sql = @"
CREATE TABLE users (
    id INT AUTO_INCREMENT,
    name VARCHAR(100)
)
";
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal(2, result.Columns.Count);
        Assert.True(result.Columns[0].IsAutoIncrement);
        Assert.Null(result.Columns[0].IdentitySeed);
        Assert.Null(result.Columns[0].IdentityIncrement);
        Assert.False(result.Columns[1].IsAutoIncrement);
    }

    [Fact]
    public void AutoIncrement_SqlServerIdentity()
    {
        const string sql = @"
CREATE TABLE orders (
    id INT IDENTITY(1,1),
    total DECIMAL(10,2)
)
";
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal(2, result.Columns.Count);
        Assert.True(result.Columns[0].IsAutoIncrement);
        Assert.Equal(1, result.Columns[0].IdentitySeed);
        Assert.Equal(1, result.Columns[0].IdentityIncrement);
        Assert.False(result.Columns[1].IsAutoIncrement);
    }

    [Fact]
    public void AutoIncrement_SqlServerIdentity_CustomSeedIncrement()
    {
        const string sql = @"
CREATE TABLE events (
    id INT IDENTITY(100,5)
)
";
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Single(result.Columns);
        Assert.True(result.Columns[0].IsAutoIncrement);
        Assert.Equal(100, result.Columns[0].IdentitySeed);
        Assert.Equal(5, result.Columns[0].IdentityIncrement);
    }

    [Fact]
    public void AutoIncrement_PostgresSerial()
    {
        const string sql = @"
CREATE TABLE products (
    id SERIAL,
    name VARCHAR(255)
)
";
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal(2, result.Columns.Count);
        Assert.True(result.Columns[0].IsAutoIncrement);
        Assert.Equal("SERIAL", result.Columns[0].DataType.Name);
        Assert.Null(result.Columns[0].IdentitySeed);
        Assert.Null(result.Columns[0].IdentityIncrement);
        Assert.False(result.Columns[1].IsAutoIncrement);
    }

    [Fact]
    public void AutoIncrement_PostgresBigSerial()
    {
        const string sql = @"
CREATE TABLE logs (
    id BIGSERIAL,
    message VARCHAR(500)
)
";
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal(2, result.Columns.Count);
        Assert.True(result.Columns[0].IsAutoIncrement);
        Assert.Equal("BIGSERIAL", result.Columns[0].DataType.Name);
    }

    [Fact]
    public void AutoIncrement_GeneratedAlwaysAsIdentity()
    {
        const string sql = @"
CREATE TABLE employees (
    id INT GENERATED ALWAYS AS IDENTITY,
    name VARCHAR(100)
)
";
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal(2, result.Columns.Count);
        Assert.True(result.Columns[0].IsAutoIncrement);
        Assert.Null(result.Columns[0].IdentitySeed);
        Assert.Null(result.Columns[0].IdentityIncrement);
        Assert.False(result.Columns[1].IsAutoIncrement);
    }

    [Fact]
    public void AutoIncrement_NonAutoIncrementColumn_IsFalse()
    {
        const string sql = @"
CREATE TABLE items (
    id INTEGER,
    label VARCHAR(50)
)
";
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.All(result.Columns, col => Assert.False(col.IsAutoIncrement));
    }
}
