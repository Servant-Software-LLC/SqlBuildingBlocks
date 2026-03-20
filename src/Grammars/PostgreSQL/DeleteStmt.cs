using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.PostgreSQL;

/// <summary>
/// PostgreSQL-specific DELETE statement that supports the full RETURNING clause.
/// RETURNING appears after the WHERE clause.
/// </summary>
public class DeleteStmt : SqlBuildingBlocks.DeleteStmt
{
    private const string DeleteTargetOptTermName = "pgDeleteTargetOpt";

    private readonly ReturningClauseOpt pgReturningClauseOpt;

    public DeleteStmt(Grammar grammar, SqlBuildingBlocks.TableName tableName,
                      WhereClauseOpt whereClauseOpt,
                      SqlBuildingBlocks.ReturningClauseOpt returningClauseOpt,
                      JoinChainOpt joinChainOpt,
                      ReturningClauseOpt pgReturningClauseOpt)
        : base(grammar, tableName, whereClauseOpt, returningClauseOpt, joinChainOpt)
    {
        this.pgReturningClauseOpt = pgReturningClauseOpt;

        // Replace the base rule to use PostgreSQL RETURNING instead of the base one.
        // Base Rule: DELETE + deleteTargetOpt + FROM + tableName + joinChainOpt + whereClauseOpt + returningClauseOpt
        // New Rule:  DELETE + deleteTargetOpt + FROM + tableName + joinChainOpt + whereClauseOpt + pgReturningClauseOpt
        var DELETE = grammar.ToTerm("DELETE");
        var FROM = grammar.ToTerm("FROM");

        var pgDeleteTargetOpt = new NonTerminal(DeleteTargetOptTermName);
        pgDeleteTargetOpt.Rule = grammar.Empty | tableName;

        Rule = DELETE + pgDeleteTargetOpt + FROM + tableName + joinChainOpt + whereClauseOpt + pgReturningClauseOpt;
    }

    public override void Update(ParseTreeNode deleteStmt, SqlDeleteDefinition sqlDeleteDefinition)
    {
        // Our rule: DELETE + deleteTargetOpt + FROM + tableName + joinChainOpt + whereClauseOpt + pgReturningClauseOpt
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
        sqlDeleteDefinition.WhereClause = WhereClauseOpt.Create(deleteStmt.ChildNodes[5]);

        // Process the PostgreSQL RETURNING clause
        sqlDeleteDefinition.ReturningClause = pgReturningClauseOpt.Create(deleteStmt.ChildNodes[6]);
    }
}
