using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.MySQL;

/// <summary>
/// MySQL-specific INSERT statement that supports ON DUPLICATE KEY UPDATE.
/// </summary>
public class InsertStmt : SqlBuildingBlocks.InsertStmt
{
    private const string OnDuplicateKeyOptTermName = "onDuplicateKeyOpt";

    public InsertStmt(Grammar grammar, Id id, Expr expr, SelectStmt selectStmt)
        : base(grammar, id, expr, selectStmt)
    {
        var ON = grammar.ToTerm("ON");
        var DUPLICATE = grammar.ToTerm("DUPLICATE");
        var KEY = grammar.ToTerm("KEY");
        var UPDATE = grammar.ToTerm("UPDATE");

        var assignment = new NonTerminal("dupKeyAssignment");
        assignment.Rule = expr.Id + "=" + expr;

        var assignList = new NonTerminal("dupKeyAssignList");
        assignList.Rule = grammar.MakePlusRule(assignList, grammar.ToTerm(","), assignment);

        var onDuplicateKeyOpt = new NonTerminal(OnDuplicateKeyOptTermName);
        onDuplicateKeyOpt.Rule = grammar.Empty | ON + DUPLICATE + KEY + UPDATE + assignList;

        Rule = Rule + onDuplicateKeyOpt;
    }

    public override void Update(ParseTreeNode insertStmt, SqlInsertDefinition sqlInsertDefinition)
    {
        base.Update(insertStmt, sqlInsertDefinition);

        // The ON DUPLICATE KEY UPDATE clause is the last child node
        var onDuplicateKeyOpt = insertStmt.ChildNodes[insertStmt.ChildNodes.Count - 1];
        if (onDuplicateKeyOpt.Term.Name == OnDuplicateKeyOptTermName && onDuplicateKeyOpt.ChildNodes.Count > 0)
        {
            var upsertClause = new SqlUpsertClause { Action = SqlUpsertAction.Update };

            // assignList is the last child in: ON DUPLICATE KEY UPDATE assignList
            var assignList = onDuplicateKeyOpt.ChildNodes[onDuplicateKeyOpt.ChildNodes.Count - 1];
            foreach (ParseTreeNode assignmentNode in assignList.ChildNodes)
            {
                var sqlColumn = Id.CreateColumn(assignmentNode.ChildNodes[0]);
                var sqlExpression = Expr.Create(assignmentNode.ChildNodes[2]);
                upsertClause.Assignments.Add(new SqlAssignment(sqlColumn, sqlExpression));
            }

            sqlInsertDefinition.UpsertClause = upsertClause;
        }
    }
}
