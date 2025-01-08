using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class ConstraintDefTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            Id id = new(this, simpleId);

            ConstraintDef constraintDef = new(this, id);

            Root = constraintDef;
        }

        public virtual (SqlConstraintDefinition Constraint, bool Handled) Create(ParseTreeNode createTableStmt, IList<SqlColumnDefinition> columns) =>
            ((ConstraintDef)Root).Create(createTableStmt, columns);

    }

/*
    [Fact]
    public void ConstraintDef_ShouldParsePrimaryKeyConstraint()
    {
        // Arrange
        var grammar = new TestGrammar();
        var sql = "CONSTRAINT PK_Person PRIMARY KEY (ID, LastName)";
        var node = GrammarParser.Parse(grammar, sql);

        // Act
        List<SqlColumnDefinition> sqlColumnDefinitions = new();
        var result = grammar.Create(node, sqlColumnDefinitions);

        // Assert
        Assert.NotNull(result.Constraint);
        Assert.True(result.Handled);
        Assert.Equal("PK_Person", result.Constraint.Name);
        Assert.Equal("PRIMARY KEY", result.Constraint.Type);
        Assert.Equal(new List<string> { "ID", "LastName" }, result.Constraint.Columns);
    }

    [Fact]
    public void ConstraintDef_ShouldParseForeignKeyConstraint()
    {
        // Arrange
        var grammar = new TestGrammar();
        var sql = "CONSTRAINT FK_PersonOrder FOREIGN KEY (PersonID) REFERENCES Persons(PersonID)";
        var node = GrammarParser.Parse(grammar, sql);

        // Act
        List<SqlColumnDefinition> sqlColumnDefinitions = new();
        var result = grammar.Create(node, sqlColumnDefinitions);

        // Assert
        Assert.NotNull(result.Constraint);
        Assert.True(result.Handled);
        Assert.Equal("FK_PersonOrder", result.Constraint.Name);
        Assert.Equal("FOREIGN KEY", result.Constraint.Type);
        Assert.Equal(new List<string> { "PersonID" }, result.Constraint.Columns);
    }
*/
}
