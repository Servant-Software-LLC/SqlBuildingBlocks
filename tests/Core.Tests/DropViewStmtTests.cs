using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class DropViewStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            Id id = new(this, simpleId);

            DropViewStmt dropViewStmt = new(this, id);

            Root = dropViewStmt;
        }

        public virtual SqlDropViewDefinition Create(ParseTreeNode node) =>
            ((DropViewStmt)Root).Create(node);
    }

    [Fact]
    public void DropView_Simple()
    {
        //Setup
        const string sql = "DROP VIEW old_report_view";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.False(result.IfExists);
        Assert.Single(result.Views);
        Assert.Equal("old_report_view", result.Views[0].TableName);
    }

    [Fact]
    public void DropView_IfExists()
    {
        //Setup
        const string sql = "DROP VIEW IF EXISTS old_report_view";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.True(result.IfExists);
        Assert.Single(result.Views);
        Assert.Equal("old_report_view", result.Views[0].TableName);
    }

    [Fact]
    public void DropView_MultipleViews()
    {
        //Setup
        const string sql = "DROP VIEW view1, view2, view3";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.False(result.IfExists);
        Assert.Equal(3, result.Views.Count);
        Assert.Equal("view1", result.Views[0].TableName);
        Assert.Equal("view2", result.Views[1].TableName);
        Assert.Equal("view3", result.Views[2].TableName);
    }

    [Fact]
    public void DropView_QualifiedName()
    {
        //Setup
        const string sql = "DROP VIEW mydb.customer_view";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.False(result.IfExists);
        Assert.Single(result.Views);
        Assert.Equal("mydb", result.Views[0].DatabaseName);
        Assert.Equal("customer_view", result.Views[0].TableName);
    }
}
