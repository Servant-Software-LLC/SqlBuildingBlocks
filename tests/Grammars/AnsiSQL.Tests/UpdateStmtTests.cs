using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.AnsiSQL.Tests;

public class UpdateStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar() : base(false) // SQL is case insensitive
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
            AnsiSQL.SelectStmt selectStmt =
                new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            UpdateStmt updateStmt =
                new(this, id, literalValue, parameter, funcCall, tableName, whereClauseOpt, joinChainOpt);

            Root = updateStmt;
        }

        public virtual SqlUpdateDefinition Create(ParseTreeNode updateStmt) =>
            ((UpdateStmt)Root).Create(updateStmt);
    }

    [Fact]
    public void Update_WithWhere()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE locations SET zip = 32655 WHERE city = 'Boston'");

        var updateStmt = grammar.Create(node);

        Assert.NotNull(updateStmt.Table);
        Assert.Equal("locations", updateStmt.Table!.TableName);

        Assert.Single(updateStmt.Assignments);
        Assert.Equal("zip", updateStmt.Assignments[0].Column.ColumnName);
        Assert.Equal(32655, updateStmt.Assignments[0].Value!.Int);

        Assert.NotNull(updateStmt.WhereClause);
        Assert.Equal("city", updateStmt.WhereClause!.BinExpr!.Left.Column!.ColumnName);
        Assert.Equal("Boston", updateStmt.WhereClause.BinExpr.Right!.Value!.String);
    }

    [Fact]
    public void Update_MultipleAssignments()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "UPDATE Customers SET City = 'Boston', State = 'MA' WHERE ID = 1");

        var updateStmt = grammar.Create(node);

        Assert.Equal(2, updateStmt.Assignments.Count);
        Assert.Equal("City", updateStmt.Assignments[0].Column.ColumnName);
        Assert.Equal("Boston", updateStmt.Assignments[0].Value!.String);
        Assert.Equal("State", updateStmt.Assignments[1].Column.ColumnName);
        Assert.Equal("MA", updateStmt.Assignments[1].Value!.String);
    }

    [Fact]
    public void Update_WithoutWhere()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE Customers SET Status = 'Active'");

        var updateStmt = grammar.Create(node);

        Assert.Equal("Customers", updateStmt.Table!.TableName);
        Assert.Single(updateStmt.Assignments);
        Assert.Null(updateStmt.WhereClause);
    }
}
