using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Grammars.SQLServer.Tests;

public class MergeStmtTests
{
    #region Test Grammar

    private class MergeTestGrammar : Grammar
    {
        public MergeTestGrammar() : base(false) // SQL is case insensitive
        {
            SQLServer.SimpleId simpleId = new(this);
            AliasOpt aliasOpt = new(this, simpleId);
            Id id = new(this, simpleId);
            LiteralValue literalValue = new(this);
            TableHintOpt tableHintOpt = new(this);
            TableName tableName = new(this, aliasOpt, id, tableHintOpt);
            Parameter parameter = new(this);
            Expr expr = new(this, id, literalValue, parameter);
            FuncCall funcCall = new(this, id, expr);
            JoinChainOpt joinChainOpt = new(this, tableName, expr);
            WhereClauseOpt whereClauseOpt = new(this, expr);
            OrderByList orderByList = new(this, id);
            SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);
            OutputClauseOpt outputClauseOpt = new(this, id);
            MergeStmt mergeStmt = new(this, id, expr, tableName, funcCall, outputClauseOpt);

            expr.InitializeRule(selectStmt, funcCall);

            Root = mergeStmt;
        }

        public SqlMergeDefinition Create(ParseTreeNode mergeStmt) =>
            ((MergeStmt)Root).Create(mergeStmt);
    }

    #endregion

    #region WHEN MATCHED THEN UPDATE

    [Fact]
    public void Merge_WhenMatched_Update()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO [Target] USING [Source] ON [Target].[ID] = [Source].[ID] " +
            "WHEN MATCHED THEN UPDATE SET [Target].[Name] = [Source].[Name], [Target].[Price] = [Source].[Price]");

        var merge = grammar.Create(node);

        Assert.NotNull(merge.TargetTable);
        Assert.Equal("Target", merge.TargetTable!.TableName);
        Assert.NotNull(merge.SourceTable);
        Assert.Equal("Source", merge.SourceTable!.TableName);
        Assert.NotNull(merge.SearchCondition);

        Assert.Single(merge.WhenClauses);
        var when = merge.WhenClauses[0];
        Assert.Equal(SqlMergeWhenType.Matched, when.WhenType);
        Assert.Equal(SqlMergeActionType.Update, when.ActionType);
        Assert.Equal(2, when.Assignments.Count);
        Assert.Null(when.AdditionalCondition);
    }

    #endregion

    #region WHEN MATCHED THEN DELETE

    [Fact]
    public void Merge_WhenMatched_Delete()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE [Target] USING [Source] ON [Target].[ID] = [Source].[ID] " +
            "WHEN MATCHED THEN DELETE");

        var merge = grammar.Create(node);

        Assert.Equal("Target", merge.TargetTable!.TableName);
        Assert.Equal("Source", merge.SourceTable!.TableName);

        Assert.Single(merge.WhenClauses);
        var when = merge.WhenClauses[0];
        Assert.Equal(SqlMergeWhenType.Matched, when.WhenType);
        Assert.Equal(SqlMergeActionType.Delete, when.ActionType);
        Assert.Empty(when.Assignments);
    }

    #endregion

    #region WHEN NOT MATCHED THEN INSERT

    [Fact]
    public void Merge_WhenNotMatched_Insert()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO [Target] USING [Source] ON [Target].[ID] = [Source].[ID] " +
            "WHEN NOT MATCHED THEN INSERT ([ID], [Name], [Price]) VALUES ([Source].[ID], [Source].[Name], [Source].[Price])");

        var merge = grammar.Create(node);

        Assert.Single(merge.WhenClauses);
        var when = merge.WhenClauses[0];
        Assert.Equal(SqlMergeWhenType.NotMatched, when.WhenType);
        Assert.Equal(SqlMergeActionType.Insert, when.ActionType);
        Assert.Equal(3, when.InsertColumns.Count);
        Assert.Equal("ID", when.InsertColumns[0].ColumnName);
        Assert.Equal("Name", when.InsertColumns[1].ColumnName);
        Assert.Equal("Price", when.InsertColumns[2].ColumnName);
        Assert.Equal(3, when.InsertValues.Count);
    }

    [Fact]
    public void Merge_WhenNotMatchedByTarget_Insert()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO [Target] USING [Source] ON [Target].[ID] = [Source].[ID] " +
            "WHEN NOT MATCHED BY TARGET THEN INSERT ([ID], [Name]) VALUES ([Source].[ID], [Source].[Name])");

        var merge = grammar.Create(node);

        Assert.Single(merge.WhenClauses);
        var when = merge.WhenClauses[0];
        Assert.Equal(SqlMergeWhenType.NotMatched, when.WhenType);
        Assert.Equal(SqlMergeActionType.Insert, when.ActionType);
        Assert.Equal(2, when.InsertColumns.Count);
        Assert.Equal(2, when.InsertValues.Count);
    }

    #endregion

    #region WHEN NOT MATCHED BY SOURCE

    [Fact]
    public void Merge_WhenNotMatchedBySource_Delete()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO [Target] USING [Source] ON [Target].[ID] = [Source].[ID] " +
            "WHEN NOT MATCHED BY SOURCE THEN DELETE");

        var merge = grammar.Create(node);

        Assert.Single(merge.WhenClauses);
        var when = merge.WhenClauses[0];
        Assert.Equal(SqlMergeWhenType.NotMatchedBySource, when.WhenType);
        Assert.Equal(SqlMergeActionType.Delete, when.ActionType);
    }

    [Fact]
    public void Merge_WhenNotMatchedBySource_Update()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO [Target] USING [Source] ON [Target].[ID] = [Source].[ID] " +
            "WHEN NOT MATCHED BY SOURCE THEN UPDATE SET [Target].[Active] = 0");

        var merge = grammar.Create(node);

        Assert.Single(merge.WhenClauses);
        var when = merge.WhenClauses[0];
        Assert.Equal(SqlMergeWhenType.NotMatchedBySource, when.WhenType);
        Assert.Equal(SqlMergeActionType.Update, when.ActionType);
        Assert.Single(when.Assignments);
    }

    #endregion

    #region Combined MERGE with multiple WHEN clauses

    [Fact]
    public void Merge_Combined_MatchedUpdate_NotMatchedInsert()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO [Products] USING [StagingProducts] ON [Products].[ProductID] = [StagingProducts].[ProductID] " +
            "WHEN MATCHED THEN UPDATE SET [Products].[Name] = [StagingProducts].[Name], [Products].[Price] = [StagingProducts].[Price] " +
            "WHEN NOT MATCHED THEN INSERT ([ProductID], [Name], [Price]) VALUES ([StagingProducts].[ProductID], [StagingProducts].[Name], [StagingProducts].[Price])");

        var merge = grammar.Create(node);

        Assert.Equal("Products", merge.TargetTable!.TableName);
        Assert.Equal("StagingProducts", merge.SourceTable!.TableName);
        Assert.Equal(2, merge.WhenClauses.Count);

        var matchedClause = merge.WhenClauses[0];
        Assert.Equal(SqlMergeWhenType.Matched, matchedClause.WhenType);
        Assert.Equal(SqlMergeActionType.Update, matchedClause.ActionType);
        Assert.Equal(2, matchedClause.Assignments.Count);

        var notMatchedClause = merge.WhenClauses[1];
        Assert.Equal(SqlMergeWhenType.NotMatched, notMatchedClause.WhenType);
        Assert.Equal(SqlMergeActionType.Insert, notMatchedClause.ActionType);
        Assert.Equal(3, notMatchedClause.InsertColumns.Count);
        Assert.Equal(3, notMatchedClause.InsertValues.Count);
    }

    [Fact]
    public void Merge_AllThreeWhenClauses()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO [Target] USING [Source] ON [Target].[ID] = [Source].[ID] " +
            "WHEN MATCHED THEN UPDATE SET [Target].[Val] = [Source].[Val] " +
            "WHEN NOT MATCHED THEN INSERT ([ID], [Val]) VALUES ([Source].[ID], [Source].[Val]) " +
            "WHEN NOT MATCHED BY SOURCE THEN DELETE");

        var merge = grammar.Create(node);

        Assert.Equal(3, merge.WhenClauses.Count);

        Assert.Equal(SqlMergeWhenType.Matched, merge.WhenClauses[0].WhenType);
        Assert.Equal(SqlMergeActionType.Update, merge.WhenClauses[0].ActionType);

        Assert.Equal(SqlMergeWhenType.NotMatched, merge.WhenClauses[1].WhenType);
        Assert.Equal(SqlMergeActionType.Insert, merge.WhenClauses[1].ActionType);

        Assert.Equal(SqlMergeWhenType.NotMatchedBySource, merge.WhenClauses[2].WhenType);
        Assert.Equal(SqlMergeActionType.Delete, merge.WhenClauses[2].ActionType);
    }

    #endregion

    #region WHEN clause with additional AND condition

    [Fact]
    public void Merge_WhenMatched_WithAdditionalCondition()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO [Target] USING [Source] ON [Target].[ID] = [Source].[ID] " +
            "WHEN MATCHED AND [Source].[Price] > 0 THEN UPDATE SET [Target].[Price] = [Source].[Price]");

        var merge = grammar.Create(node);

        Assert.Single(merge.WhenClauses);
        var when = merge.WhenClauses[0];
        Assert.Equal(SqlMergeWhenType.Matched, when.WhenType);
        Assert.NotNull(when.AdditionalCondition);
        Assert.Equal(SqlMergeActionType.Update, when.ActionType);
    }

    [Fact]
    public void Merge_WhenNotMatchedBySource_WithAdditionalCondition()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO [Target] USING [Source] ON [Target].[ID] = [Source].[ID] " +
            "WHEN NOT MATCHED BY SOURCE AND [Target].[Active] = 1 THEN DELETE");

        var merge = grammar.Create(node);

        Assert.Single(merge.WhenClauses);
        var when = merge.WhenClauses[0];
        Assert.Equal(SqlMergeWhenType.NotMatchedBySource, when.WhenType);
        Assert.NotNull(when.AdditionalCondition);
        Assert.Equal(SqlMergeActionType.Delete, when.ActionType);
    }

    #endregion

    #region OUTPUT clause on MERGE

    [Fact]
    public void Merge_WithOutput_InsertedStar()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO [Target] USING [Source] ON [Target].[ID] = [Source].[ID] " +
            "WHEN MATCHED THEN UPDATE SET [Target].[Name] = [Source].[Name] " +
            "OUTPUT INSERTED.*");

        var merge = grammar.Create(node);

        Assert.NotNull(merge.OutputClause);
        Assert.Single(merge.OutputClause!.Columns);
        Assert.Equal("INSERTED", merge.OutputClause.Columns[0].Source);
        Assert.Null(merge.OutputClause.Columns[0].ColumnName); // wildcard
    }

    [Fact]
    public void Merge_WithOutput_SpecificColumns()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO [Target] USING [Source] ON [Target].[ID] = [Source].[ID] " +
            "WHEN MATCHED THEN UPDATE SET [Target].[Name] = [Source].[Name] " +
            "WHEN NOT MATCHED THEN INSERT ([ID], [Name]) VALUES ([Source].[ID], [Source].[Name]) " +
            "OUTPUT INSERTED.[ID], DELETED.[Name]");

        var merge = grammar.Create(node);

        Assert.NotNull(merge.OutputClause);
        Assert.Equal(2, merge.OutputClause!.Columns.Count);
        Assert.Equal("INSERTED", merge.OutputClause.Columns[0].Source);
        Assert.Equal("ID", merge.OutputClause.Columns[0].ColumnName);
        Assert.Equal("DELETED", merge.OutputClause.Columns[1].Source);
        Assert.Equal("Name", merge.OutputClause.Columns[1].ColumnName);
    }

    [Fact]
    public void Merge_WithOutput_IntoTable()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO [Target] USING [Source] ON [Target].[ID] = [Source].[ID] " +
            "WHEN MATCHED THEN DELETE " +
            "OUTPUT DELETED.* INTO [AuditLog]");

        var merge = grammar.Create(node);

        Assert.NotNull(merge.OutputClause);
        Assert.Single(merge.OutputClause!.Columns);
        Assert.NotNull(merge.OutputClause.IntoTable);
        Assert.Equal("AuditLog", merge.OutputClause.IntoTable!.TableName);
    }

    [Fact]
    public void Merge_WithoutOutput()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE INTO [Target] USING [Source] ON [Target].[ID] = [Source].[ID] " +
            "WHEN MATCHED THEN UPDATE SET [Target].[Name] = [Source].[Name]");

        var merge = grammar.Create(node);

        Assert.Null(merge.OutputClause);
    }

    #endregion

    #region Case insensitivity

    [Fact]
    public void Merge_CaseInsensitive()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "merge into [Target] using [Source] on [Target].[ID] = [Source].[ID] " +
            "when matched then update set [Target].[Name] = [Source].[Name] " +
            "when not matched then insert ([ID]) values ([Source].[ID])");

        var merge = grammar.Create(node);

        Assert.Equal("Target", merge.TargetTable!.TableName);
        Assert.Equal("Source", merge.SourceTable!.TableName);
        Assert.Equal(2, merge.WhenClauses.Count);
    }

    #endregion

    #region Without INTO keyword

    [Fact]
    public void Merge_WithoutInto()
    {
        MergeTestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar,
            "MERGE [Target] USING [Source] ON [Target].[ID] = [Source].[ID] " +
            "WHEN MATCHED THEN UPDATE SET [Target].[Name] = [Source].[Name]");

        var merge = grammar.Create(node);

        Assert.Equal("Target", merge.TargetTable!.TableName);
        Assert.Equal("Source", merge.SourceTable!.TableName);
        Assert.Single(merge.WhenClauses);
    }

    #endregion
}
