using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class AlterViewStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            Id id = new(this, simpleId);
            SelectStmt selectStmt = new(this, id);
            FuncCall funcCall = new(this, selectStmt.JoinChainOpt.Expr.Id, selectStmt.JoinChainOpt.Expr);
            selectStmt.Expr.InitializeRule(selectStmt, funcCall);

            AlterViewStmt alterViewStmt = new(this, id, selectStmt);

            Root = alterViewStmt;
        }

        public virtual SqlAlterViewDefinition Create(ParseTreeNode node) =>
            ((AlterViewStmt)Root).Create(node);
    }

    [Fact]
    public void AlterView_Simple()
    {
        //Setup
        const string sql = "ALTER VIEW active_customers AS SELECT * FROM customers WHERE status = 'active' AND verified = 1";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.NotNull(result.View);
        Assert.Equal("active_customers", result.View!.TableName);
        Assert.NotNull(result.AsSelect);
    }

    [Fact]
    public void AlterView_QualifiedName()
    {
        //Setup
        const string sql = "ALTER VIEW mydb.report_view AS SELECT id, name FROM customers";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        //Act
        var result = grammar.Create(node);

        //Assert
        Assert.Equal("mydb", result.View!.DatabaseName);
        Assert.Equal("report_view", result.View.TableName);
        Assert.NotNull(result.AsSelect);
    }
}
