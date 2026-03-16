using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class OrderByListTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            var simpleId = new SimpleId(this);
            var id = new Id(this, simpleId);
            var orderByList = new OrderByList(this, id);

            Root = orderByList;
        }
    }

    [Fact]
    public void SelectStmt_OrderBy_SingleColumn_Asc_IsPopulated()
    {
        SelectStmtTests.TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT id, city FROM locations ORDER BY id ASC");

        var selectDef = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectDef.OrderBy);
        Assert.Single(selectDef.OrderBy);
        Assert.Equal("id", selectDef.OrderBy[0].ColumnName);
        Assert.False(selectDef.OrderBy[0].Descending);
    }

    [Fact]
    public void SelectStmt_OrderBy_SingleColumn_Desc_IsPopulated()
    {
        SelectStmtTests.TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT id, city FROM locations ORDER BY city DESC");

        var selectDef = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectDef.OrderBy);
        Assert.Single(selectDef.OrderBy);
        Assert.Equal("city", selectDef.OrderBy[0].ColumnName);
        Assert.True(selectDef.OrderBy[0].Descending);
    }

    [Fact]
    public void SelectStmt_OrderBy_MultiColumn_IsPopulated()
    {
        SelectStmtTests.TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT id, city, state FROM locations ORDER BY state ASC, city DESC");

        var selectDef = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectDef.OrderBy);
        Assert.Equal(2, selectDef.OrderBy.Count);

        Assert.Equal("state", selectDef.OrderBy[0].ColumnName);
        Assert.False(selectDef.OrderBy[0].Descending);

        Assert.Equal("city", selectDef.OrderBy[1].ColumnName);
        Assert.True(selectDef.OrderBy[1].Descending);
    }

    [Fact]
    public void SelectStmt_OrderBy_TableQualified_IsPopulated()
    {
        SelectStmtTests.TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT l.id, l.city FROM locations l ORDER BY l.id ASC");

        var selectDef = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectDef.OrderBy);
        Assert.Single(selectDef.OrderBy);
        Assert.Equal("l.id", selectDef.OrderBy[0].ColumnName);
        Assert.False(selectDef.OrderBy[0].Descending);
    }

    [Fact]
    public void SelectStmt_NoOrderBy_ReturnsEmptyList()
    {
        SelectStmtTests.TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT id, city FROM locations");

        var selectDef = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectDef.OrderBy);
        Assert.Empty(selectDef.OrderBy);
    }

    [Fact]
    public void SelectStmt_OrderBy_ImplicitAsc_IsNotDescending()
    {
        SelectStmtTests.TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "SELECT id, city FROM locations ORDER BY city");

        var selectDef = ((SelectStmt)grammar.Root).Create(node);

        Assert.NotNull(selectDef.OrderBy);
        Assert.Single(selectDef.OrderBy);
        Assert.Equal("city", selectDef.OrderBy[0].ColumnName);
        Assert.False(selectDef.OrderBy[0].Descending);
    }
}
