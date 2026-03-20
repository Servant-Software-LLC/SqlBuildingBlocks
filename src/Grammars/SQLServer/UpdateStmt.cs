using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.SQLServer;

/// <summary>
/// SQL Server-specific UPDATE statement that supports the OUTPUT clause.
/// OUTPUT appears after SET assignments, before the FROM/WHERE clause.
/// </summary>
public class UpdateStmt : SqlBuildingBlocks.UpdateStmt
{
    private const string UpdateSourceOptTermName = "ssUpdateSourceOpt";

    private readonly OutputClauseOpt outputClauseOpt;

    public UpdateStmt(Grammar grammar, Expr expr, FuncCall funcCall, SqlBuildingBlocks.TableName tableName,
                      WhereClauseOpt whereClauseOpt, ReturningClauseOpt returningClauseOpt,
                      JoinChainOpt joinChainOpt, OutputClauseOpt outputClauseOpt)
        : base(grammar, expr, funcCall, tableName, whereClauseOpt, returningClauseOpt, joinChainOpt)
    {
        this.outputClauseOpt = outputClauseOpt;

        // Rebuild the rule to include OUTPUT after SET assignments.
        // Base Rule: UPDATE + tableName + joinChainOpt + SET + assignList + updateSourceOpt + whereClauseOpt + returningClauseOpt
        // New Rule:  UPDATE + tableName + joinChainOpt + SET + assignList + outputClauseOpt + updateSourceOpt + whereClauseOpt
        var UPDATE = grammar.ToTerm("UPDATE");
        var SET = grammar.ToTerm("SET");
        var COMMA = grammar.ToTerm(",");
        var FROM = grammar.ToTerm("FROM");

        var ssAssignment = new NonTerminal("ssAssignment");
        ssAssignment.Rule = expr.Id + "=" + expr;

        var ssAssignList = new NonTerminal("ssAssignList");
        ssAssignList.Rule = grammar.MakePlusRule(ssAssignList, COMMA, ssAssignment);

        var ssUpdateSourceOpt = new NonTerminal(UpdateSourceOptTermName);
        ssUpdateSourceOpt.Rule = grammar.Empty | FROM + tableName + joinChainOpt;

        Rule = UPDATE + tableName + joinChainOpt + SET + ssAssignList + outputClauseOpt +
               ssUpdateSourceOpt + whereClauseOpt;
    }

    public override void Update(ParseTreeNode updateStmt, SqlUpdateDefinition sqlUpdateDefinition)
    {
        // Our rule: UPDATE + tableName + joinChainOpt + SET + assignList + outputClauseOpt + updateSourceOpt + whereClauseOpt
        // Indices:   [0]     [1]         [2]           [3]   [4]          [5]                [6]               [7]

        if (updateStmt.Term.Name != TermName)
        {
            throw new ArgumentException(
                $"Cannot create building block. The TermName for node is {updateStmt.Term.Name} " +
                $"which does not match {TermName}", nameof(updateStmt));
        }

        sqlUpdateDefinition.Table = TableName.Create(updateStmt.ChildNodes[1]);

        AddJoins(sqlUpdateDefinition, updateStmt.ChildNodes[2]);

        var assignList = updateStmt.ChildNodes[4];
        foreach (ParseTreeNode assignment in assignList.ChildNodes)
        {
            var sqlColumn = Expr.Id.CreateColumn(assignment.ChildNodes[0]);
            var sqlExpression = Expr.Create(assignment.ChildNodes[2]);
            sqlUpdateDefinition.Assignments.Add(new SqlAssignment(sqlColumn, sqlExpression));
        }

        // Process OUTPUT clause
        sqlUpdateDefinition.OutputClause = outputClauseOpt.Create(updateStmt.ChildNodes[5]);

        AddSource(sqlUpdateDefinition, updateStmt.ChildNodes[6]);

        sqlUpdateDefinition.WhereClause = WhereClauseOpt.Create(updateStmt.ChildNodes[7]);
    }

    private new void AddSource(SqlUpdateDefinition sqlUpdateDefinition, ParseTreeNode updateSourceOptNode)
    {
        if (updateSourceOptNode.Term.Name != UpdateSourceOptTermName || updateSourceOptNode.ChildNodes.Count == 0)
            return;

        sqlUpdateDefinition.SourceTable = TableName.Create(updateSourceOptNode.ChildNodes[1]);

        foreach (var join in JoinChainOpt.Create(updateSourceOptNode.ChildNodes[2]))
        {
            sqlUpdateDefinition.Joins.Add(join);
        }
    }
}
