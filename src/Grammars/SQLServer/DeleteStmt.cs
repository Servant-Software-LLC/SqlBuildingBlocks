using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.SQLServer;

/// <summary>
/// SQL Server-specific DELETE statement that supports the OUTPUT clause.
/// OUTPUT appears after FROM table, before the WHERE clause.
/// </summary>
public class DeleteStmt : SqlBuildingBlocks.DeleteStmt
{
    private const string DeleteTargetOptTermName = "ssDeleteTargetOpt";

    private readonly OutputClauseOpt outputClauseOpt;

    public DeleteStmt(Grammar grammar, SqlBuildingBlocks.TableName tableName, WhereClauseOpt whereClauseOpt,
                      ReturningClauseOpt returningClauseOpt, JoinChainOpt joinChainOpt,
                      OutputClauseOpt outputClauseOpt)
        : base(grammar, tableName, whereClauseOpt, returningClauseOpt, joinChainOpt)
    {
        this.outputClauseOpt = outputClauseOpt;

        // Rebuild the rule to include OUTPUT between table/joins and WHERE.
        // Base Rule: DELETE + deleteTargetOpt + FROM + tableName + joinChainOpt + whereClauseOpt + returningClauseOpt
        // New Rule:  DELETE + deleteTargetOpt + FROM + tableName + joinChainOpt + outputClauseOpt + whereClauseOpt
        var DELETE = grammar.ToTerm("DELETE");
        var FROM = grammar.ToTerm("FROM");

        var ssDeleteTargetOpt = new NonTerminal(DeleteTargetOptTermName);
        ssDeleteTargetOpt.Rule = grammar.Empty | tableName;

        Rule = DELETE + ssDeleteTargetOpt + FROM + tableName + joinChainOpt + outputClauseOpt + whereClauseOpt;
    }

    public override void Update(ParseTreeNode deleteStmt, SqlDeleteDefinition sqlDeleteDefinition)
    {
        // Our rule: DELETE + deleteTargetOpt + FROM + tableName + joinChainOpt + outputClauseOpt + whereClauseOpt
        // Indices:   [0]     [1]              [2]    [3]          [4]            [5]               [6]

        if (deleteStmt.Term.Name != TermName)
        {
            throw new ArgumentException(
                $"Cannot create building block. The TermName for node is {deleteStmt.Term.Name} " +
                $"which does not match {TermName}", nameof(deleteStmt));
        }

        var deleteTargetOpt = deleteStmt.ChildNodes[1];
        var deleteSourceNode = deleteStmt.ChildNodes[3];

        if (deleteTargetOpt.Term.Name == DeleteTargetOptTermName && deleteTargetOpt.ChildNodes.Count > 0)
        {
            sqlDeleteDefinition.Table = TableName.Create(deleteTargetOpt.ChildNodes[0]);
            sqlDeleteDefinition.SourceTable = TableName.Create(deleteSourceNode);
        }
        else
        {
            sqlDeleteDefinition.Table = TableName.Create(deleteSourceNode);
        }

        AddJoins(sqlDeleteDefinition, deleteStmt.ChildNodes[4]);

        // Process OUTPUT clause
        sqlDeleteDefinition.OutputClause = outputClauseOpt.Create(deleteStmt.ChildNodes[5]);

        sqlDeleteDefinition.WhereClause = WhereClauseOpt.Create(deleteStmt.ChildNodes[6]);
    }
}
