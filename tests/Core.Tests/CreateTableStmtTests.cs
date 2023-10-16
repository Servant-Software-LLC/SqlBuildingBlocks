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
        Assert.Equal(1, sqlCreateTableDefinition.Constraints.Count);
        var constraint = sqlCreateTableDefinition.Constraints[0];
        Assert.Equal("PK_SomeSetting", constraint.Name);
        Assert.Null(constraint.UniqueConstraint);
        Assert.Null(constraint.ForeignKeyConstraint);
        var primaryKeyConstraint = constraint.PrimaryKeyConstraint;
        Assert.Equal(1, primaryKeyConstraint.Columns.Count);
        Assert.Equal("Id", primaryKeyConstraint.Columns[0]);
    }
}
