using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class MergeStmtTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar() : base(false) // SQL is case-insensitive
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
            MergeStmt mergeStmt = new(this, id, expr, tableName, funcCall);

            expr.InitializeRule(selectStmt, funcCall);

            Root = mergeStmt;
        }

        public SqlMergeDefinition Create(ParseTreeNode node) =>
            ((MergeStmt)Root).Create(node);
    }

    // MergeStmt marks "TARGET" and "SOURCE" as reserved (along with MERGE/USING/MATCHED), so
    // tests bracket those identifiers when used as table names. Other identifiers (Tbl, Src, Id)
    // are unreserved.

    [Fact]
    public void Create_WhenMatched_Update_PopulatesTargetSourceAndAssignments()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO Tbl USING Src ON Tbl.Id = Src.Id " +
            "WHEN MATCHED THEN UPDATE SET Tbl.Name = Src.Name, Tbl.Price = Src.Price");

        // Act
        var merge = grammar.Create(node);

        // Assert
        Assert.NotNull(merge.TargetTable);
        Assert.Equal("Tbl", merge.TargetTable!.TableName);
        Assert.NotNull(merge.SourceTable);
        Assert.Equal("Src", merge.SourceTable!.TableName);
        Assert.NotNull(merge.SearchCondition);

        Assert.Single(merge.WhenClauses);
        var when = merge.WhenClauses[0];
        Assert.Equal(SqlMergeWhenType.Matched, when.WhenType);
        Assert.Equal(SqlMergeActionType.Update, when.ActionType);
        Assert.Equal(2, when.Assignments.Count);
        Assert.Null(when.AdditionalCondition);
    }

    [Fact]
    public void Create_WhenMatched_Delete_PopulatesDeleteAction()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE Tbl USING Src ON Tbl.Id = Src.Id " +
            "WHEN MATCHED THEN DELETE");

        // Act
        var merge = grammar.Create(node);

        // Assert
        Assert.Single(merge.WhenClauses);
        var when = merge.WhenClauses[0];
        Assert.Equal(SqlMergeWhenType.Matched, when.WhenType);
        Assert.Equal(SqlMergeActionType.Delete, when.ActionType);
        Assert.Empty(when.Assignments);
    }

    [Fact]
    public void Create_WhenNotMatched_Insert_PopulatesInsertColumnsAndValues()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO Tbl USING Src ON Tbl.Id = Src.Id " +
            "WHEN NOT MATCHED THEN INSERT (Id, Name, Price) VALUES (Src.Id, Src.Name, Src.Price)");

        // Act
        var merge = grammar.Create(node);

        // Assert
        Assert.Single(merge.WhenClauses);
        var when = merge.WhenClauses[0];
        Assert.Equal(SqlMergeWhenType.NotMatched, when.WhenType);
        Assert.Equal(SqlMergeActionType.Insert, when.ActionType);
        Assert.Equal(3, when.InsertColumns.Count);
        Assert.Equal("Id", when.InsertColumns[0].ColumnName);
        Assert.Equal("Name", when.InsertColumns[1].ColumnName);
        Assert.Equal("Price", when.InsertColumns[2].ColumnName);
        Assert.Equal(3, when.InsertValues.Count);
    }

    [Fact]
    public void Create_WhenNotMatchedBySource_Delete_SetsTypeAndAction()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO Tbl USING Src ON Tbl.Id = Src.Id " +
            "WHEN NOT MATCHED BY SOURCE THEN DELETE");

        // Act
        var merge = grammar.Create(node);

        // Assert
        Assert.Single(merge.WhenClauses);
        var when = merge.WhenClauses[0];
        Assert.Equal(SqlMergeWhenType.NotMatchedBySource, when.WhenType);
        Assert.Equal(SqlMergeActionType.Delete, when.ActionType);
    }

    [Fact]
    public void Create_AllThreeWhenClauses_PopulatesEachClause()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO Tbl USING Src ON Tbl.Id = Src.Id " +
            "WHEN MATCHED THEN UPDATE SET Tbl.Val = Src.Val " +
            "WHEN NOT MATCHED THEN INSERT (Id, Val) VALUES (Src.Id, Src.Val) " +
            "WHEN NOT MATCHED BY SOURCE THEN DELETE");

        // Act
        var merge = grammar.Create(node);

        // Assert
        Assert.Equal(3, merge.WhenClauses.Count);

        Assert.Equal(SqlMergeWhenType.Matched, merge.WhenClauses[0].WhenType);
        Assert.Equal(SqlMergeActionType.Update, merge.WhenClauses[0].ActionType);

        Assert.Equal(SqlMergeWhenType.NotMatched, merge.WhenClauses[1].WhenType);
        Assert.Equal(SqlMergeActionType.Insert, merge.WhenClauses[1].ActionType);

        Assert.Equal(SqlMergeWhenType.NotMatchedBySource, merge.WhenClauses[2].WhenType);
        Assert.Equal(SqlMergeActionType.Delete, merge.WhenClauses[2].ActionType);
    }

    [Fact]
    public void Create_WhenMatched_WithAdditionalCondition_PopulatesCondition()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO Tbl USING Src ON Tbl.Id = Src.Id " +
            "WHEN MATCHED AND Src.Price > 0 THEN UPDATE SET Tbl.Price = Src.Price");

        // Act
        var merge = grammar.Create(node);

        // Assert
        Assert.Single(merge.WhenClauses);
        var when = merge.WhenClauses[0];
        Assert.Equal(SqlMergeWhenType.Matched, when.WhenType);
        Assert.NotNull(when.AdditionalCondition);
        Assert.Equal(SqlMergeActionType.Update, when.ActionType);
    }

    [Fact]
    public void Create_WithoutInto_StillParsesAndCreates()
    {
        // Arrange
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE Tbl USING Src ON Tbl.Id = Src.Id " +
            "WHEN MATCHED THEN UPDATE SET Tbl.Name = Src.Name");

        // Act
        var merge = grammar.Create(node);

        // Assert
        Assert.Equal("Tbl", merge.TargetTable!.TableName);
        Assert.Equal("Src", merge.SourceTable!.TableName);
        Assert.Single(merge.WhenClauses);
    }

    [Fact]
    public void Constructor_WithNullId_ThrowsArgumentNullException()
    {
        // Arrange
        var grammar = new Grammar(false);
        SimpleId simpleId = new(grammar);
        AliasOpt aliasOpt = new(grammar, simpleId);
        Id id = new(grammar, simpleId);
        LiteralValue literalValue = new(grammar);
        TableName tableName = new(grammar, aliasOpt, id);
        Parameter parameter = new(grammar);
        Expr expr = new(grammar, id, literalValue, parameter);
        FuncCall funcCall = new(grammar, id, expr);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MergeStmt(grammar, null!, expr, tableName, funcCall));
    }
}
