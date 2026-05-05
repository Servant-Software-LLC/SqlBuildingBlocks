#nullable enable
using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class ColumnDefTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            Id id = new(this, simpleId);
            DataType dataType = new(this);
            ColumnDef columnDef = new(this, id, dataType);

            Root = columnDef;

            // Match punctuation stripping that CreateTableStmt applies in production
            MarkPunctuation("(", ")");
        }

        public SqlColumnDefinition? Create(ParseTreeNode node, IList<SqlConstraintDefinition> constraints) =>
            ((ColumnDef)Root).Create(node, constraints);
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
            ColumnDef columnDef = new(this, id, dataType, expr);

            Root = columnDef;

            MarkPunctuation("(", ")");
        }

        public SqlColumnDefinition? Create(ParseTreeNode node, IList<SqlConstraintDefinition> constraints) =>
            ((ColumnDef)Root).Create(node, constraints);
    }

    [Fact]
    public void Create_BasicIntegerColumn_ReturnsDefinition()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "Id INTEGER");
        var constraints = new List<SqlConstraintDefinition>();

        // Act
        var columnDef = grammar.Create(node, constraints);

        // Assert
        Assert.NotNull(columnDef);
        Assert.Equal("Id", columnDef!.ColumnName);
        Assert.Equal("INTEGER", columnDef.DataType.Name);
        Assert.True(columnDef.AllowNulls);
        Assert.False(columnDef.IsAutoIncrement);
        Assert.Null(columnDef.DefaultLiteralValue);
        Assert.Empty(constraints);
    }

    [Fact]
    public void Create_NotNullColumn_SetsAllowNullsFalse()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "Name VARCHAR(50) NOT NULL");
        var constraints = new List<SqlConstraintDefinition>();

        // Act
        var columnDef = grammar.Create(node, constraints);

        // Assert
        Assert.NotNull(columnDef);
        Assert.Equal("Name", columnDef!.ColumnName);
        Assert.False(columnDef.AllowNulls);
    }

    [Fact]
    public void Create_NullExplicit_SetsAllowNullsTrue()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "Name VARCHAR(50) NULL");
        var constraints = new List<SqlConstraintDefinition>();

        // Act
        var columnDef = grammar.Create(node, constraints);

        // Assert
        Assert.NotNull(columnDef);
        Assert.True(columnDef!.AllowNulls);
    }

    [Fact]
    public void Create_WithDefaultLiteral_SetsDefaultLiteralValue()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "Status VARCHAR(20) DEFAULT 'active'");
        var constraints = new List<SqlConstraintDefinition>();

        // Act
        var columnDef = grammar.Create(node, constraints);

        // Assert
        Assert.NotNull(columnDef);
        Assert.NotNull(columnDef!.DefaultLiteralValue);
        Assert.Equal("active", columnDef.DefaultLiteralValue!.String);
        Assert.Null(columnDef.DefaultFunctionValue);
    }

    [Fact]
    public void Create_WithDefaultFunctionCall_SetsDefaultFunctionValue()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "Created TIMESTAMP DEFAULT GETDATE()");
        var constraints = new List<SqlConstraintDefinition>();

        // Act
        var columnDef = grammar.Create(node, constraints);

        // Assert
        Assert.NotNull(columnDef);
        Assert.NotNull(columnDef!.DefaultFunctionValue);
        Assert.Equal("GETDATE", columnDef.DefaultFunctionValue!.FunctionName);
        Assert.Null(columnDef.DefaultLiteralValue);
    }

    [Fact]
    public void Create_WithPrimaryKey_AddsPrimaryKeyConstraint()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "Id INTEGER PRIMARY KEY");
        var constraints = new List<SqlConstraintDefinition>();

        // Act
        var columnDef = grammar.Create(node, constraints);

        // Assert
        Assert.NotNull(columnDef);
        Assert.Single(constraints);
        Assert.Equal("PK_Id", constraints[0].Name);
        Assert.NotNull(constraints[0].PrimaryKeyConstraint);
        Assert.Contains("Id", constraints[0].PrimaryKeyConstraint!.Columns);
    }

    [Fact]
    public void Create_WithUnique_AddsUniqueConstraint()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "Email VARCHAR(255) UNIQUE");
        var constraints = new List<SqlConstraintDefinition>();

        // Act
        var columnDef = grammar.Create(node, constraints);

        // Assert
        Assert.NotNull(columnDef);
        Assert.Single(constraints);
        Assert.Equal("UQ_Email", constraints[0].Name);
        Assert.NotNull(constraints[0].UniqueConstraint);
        Assert.Contains("Email", constraints[0].UniqueConstraint!.Columns);
    }

    [Fact]
    public void Create_WithAutoIncrement_SetsIsAutoIncrement()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "Id INTEGER AUTO_INCREMENT");
        var constraints = new List<SqlConstraintDefinition>();

        // Act
        var columnDef = grammar.Create(node, constraints);

        // Assert
        Assert.NotNull(columnDef);
        Assert.True(columnDef!.IsAutoIncrement);
        Assert.Null(columnDef.IdentitySeed);
        Assert.Null(columnDef.IdentityIncrement);
    }

    [Fact]
    public void Create_WithIdentitySpec_SetsSeedAndIncrement()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "Id INTEGER IDENTITY(1, 1)");
        var constraints = new List<SqlConstraintDefinition>();

        // Act
        var columnDef = grammar.Create(node, constraints);

        // Assert
        Assert.NotNull(columnDef);
        Assert.True(columnDef!.IsAutoIncrement);
        Assert.Equal(1, columnDef.IdentitySeed);
        Assert.Equal(1, columnDef.IdentityIncrement);
    }

    [Fact]
    public void Create_SerialDataType_SetsIsAutoIncrement()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "Id SERIAL");
        var constraints = new List<SqlConstraintDefinition>();

        // Act
        var columnDef = grammar.Create(node, constraints);

        // Assert
        Assert.NotNull(columnDef);
        Assert.True(columnDef!.IsAutoIncrement);
        Assert.Equal("SERIAL", columnDef.DataType.Name);
    }

    [Fact]
    public void Create_WithCheckConstraint_AddsCheckConstraint()
    {
        // Arrange
        CheckTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "Age INTEGER CHECK (Age >= 18)");
        var constraints = new List<SqlConstraintDefinition>();

        // Act
        var columnDef = grammar.Create(node, constraints);

        // Assert
        Assert.NotNull(columnDef);
        Assert.Single(constraints);
        Assert.Equal("CK_Age", constraints[0].Name);
        Assert.NotNull(constraints[0].CheckConstraint);
    }

    [Fact]
    public void Constructor_WithNullId_ThrowsArgumentNullException()
    {
        // Arrange
        var grammar = new Grammar();
        var dataType = new DataType(grammar);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ColumnDef(grammar, null!, dataType));
    }
}
