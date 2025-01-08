using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.SQLServer.Tests;

public class CreateTableStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SQLServer.SimpleId simpleId = new(this);
            Id id = new(this, simpleId);
            SQLServer.DataType dataType = new(this);

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
CREATE TABLE ""SomeSetting"" (
    ""Id"" INT NOT NULL,
    ""SomeProperty"" TEXT NULL,
    [Flags] BIT(16),
    CONSTRAINT UC_Id UNIQUE (Id)
)
";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var sqlCreateTableDefinition = grammar.Create(node);

        //Assert
        Assert.Equal(3, sqlCreateTableDefinition.Columns.Count);

        //Id column
        Assert.Equal("Id", sqlCreateTableDefinition.Columns[0].ColumnName);
        Assert.Equal("INT", sqlCreateTableDefinition.Columns[0].DataType.Name);
        Assert.False(sqlCreateTableDefinition.Columns[0].AllowNulls);
        Assert.False(sqlCreateTableDefinition.Columns[0].DataType.Length.HasValue);
        Assert.False(sqlCreateTableDefinition.Columns[0].DataType.Precision.HasValue);
        Assert.False(sqlCreateTableDefinition.Columns[0].DataType.Scale.HasValue);

        //SomeProperty column
        Assert.Equal("SomeProperty", sqlCreateTableDefinition.Columns[1].ColumnName);
        Assert.Equal("TEXT", sqlCreateTableDefinition.Columns[1].DataType.Name);
        Assert.True(sqlCreateTableDefinition.Columns[1].AllowNulls);
        Assert.False(sqlCreateTableDefinition.Columns[1].DataType.Length.HasValue);
        Assert.False(sqlCreateTableDefinition.Columns[1].DataType.Precision.HasValue);
        Assert.False(sqlCreateTableDefinition.Columns[1].DataType.Scale.HasValue);

        //Flags column
        Assert.Equal("Flags", sqlCreateTableDefinition.Columns[2].ColumnName);
        Assert.Equal("BIT", sqlCreateTableDefinition.Columns[2].DataType.Name);
        Assert.True(sqlCreateTableDefinition.Columns[2].AllowNulls);
        Assert.Equal(16, sqlCreateTableDefinition.Columns[2].DataType.Length.Value);
        Assert.False(sqlCreateTableDefinition.Columns[2].DataType.Precision.HasValue);
        Assert.False(sqlCreateTableDefinition.Columns[2].DataType.Scale.HasValue);

        //Constraints
        Assert.Single(sqlCreateTableDefinition.Constraints);
        var constraint = sqlCreateTableDefinition.Constraints[0];
        Assert.Equal("UC_Id", constraint.Name);
        Assert.Null(constraint.ForeignKeyConstraint);
        Assert.Null(constraint.PrimaryKeyConstraint);
        var uniqueKeyConstraint = constraint.UniqueConstraint;
        Assert.Single(uniqueKeyConstraint.Columns);
        Assert.Equal("Id", uniqueKeyConstraint.Columns[0]);
    }
}
