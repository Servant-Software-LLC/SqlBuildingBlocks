using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class UpdateStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
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

            UpdateStmt updateStmt = new(this, id, literalValue, parameter, funcCall, tableName, whereClauseOpt);

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
        var sqlUpdateDefinition = grammar.Create(node);

        //Assert - Table
        Assert.NotNull(sqlUpdateDefinition.Table);
        Assert.Equal("locations", sqlUpdateDefinition.Table.TableName);

        //Assert - SET
        var assignments = sqlUpdateDefinition.Assignments;
        Assert.Single(assignments);
        Assert.Equal("zip", assignments[0].Column.ColumnName);
        Assert.Equal(32655, assignments[0].Value.Int);

        //Assert - WHERE
        var whereClause = sqlUpdateDefinition.WhereClause;
        Assert.NotNull(whereClause);
        Assert.Equal("city", whereClause.Left.Column.ColumnName);
        Assert.Equal("Boston", whereClause.Right.Value.String);

        Assert.Null(sqlUpdateDefinition.Returning);
    }

    [Fact]
    public void Update_SetWithParameter()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE locations SET zip = @Zip");
        var sqlUpdateDefinition = grammar.Create(node);

        //Assert - Table
        Assert.NotNull(sqlUpdateDefinition.Table);
        Assert.Equal("locations", sqlUpdateDefinition.Table.TableName);

        //Assert - SET
        var assignments = sqlUpdateDefinition.Assignments;
        Assert.Single(assignments);
        Assert.Equal("zip", assignments[0].Column.ColumnName);
        Assert.Equal("Zip", assignments[0].Parameter.Name);
    }

    [Fact]
    public void Update_SetWithFunction()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE locations SET zip = ROW_COUNT()");
        var sqlUpdateDefinition = grammar.Create(node);

        //Assert - Table
        Assert.NotNull(sqlUpdateDefinition.Table);
        Assert.Equal("locations", sqlUpdateDefinition.Table.TableName);

        //Assert - SET
        var assignments = sqlUpdateDefinition.Assignments;
        Assert.Single(assignments);
        Assert.Equal("zip", assignments[0].Column.ColumnName);
        Assert.Equal("ROW_COUNT", assignments[0].Function.FunctionName);
    }

    [Fact]
    public void Update_WithIntReturingClause()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE SomeSetting SET SomeProperty = @p0 WHERE Id = @p1 RETURNING 1");
        var sqlUpdateDefinition = grammar.Create(node);

        //Assert
        Assert.NotNull(sqlUpdateDefinition.Returning);
        Assert.Equal(1, sqlUpdateDefinition.Returning.Int);
    }

    [Fact]
    public void Update_WithColumnReturingClause()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "UPDATE SomeSetting SET SomeProperty = @p0 WHERE Id = @p1 RETURNING Id");
        var sqlUpdateDefinition = grammar.Create(node);

        //Assert
        Assert.NotNull(sqlUpdateDefinition.Returning);
        Assert.NotNull(sqlUpdateDefinition.Returning.Column);
        Assert.Equal("Id", sqlUpdateDefinition.Returning.Column.ColumnName);
    }

}
