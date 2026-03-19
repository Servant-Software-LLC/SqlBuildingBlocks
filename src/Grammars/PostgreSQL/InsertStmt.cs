using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.PostgreSQL;

/// <summary>
/// PostgreSQL-specific INSERT statement that supports ON CONFLICT DO UPDATE SET ... / DO NOTHING.
/// </summary>
public class InsertStmt : SqlBuildingBlocks.InsertStmt
{
    private const string OnConflictOptTermName = "onConflictOpt";

    public InsertStmt(Grammar grammar, Id id, Expr expr, SelectStmt selectStmt)
        : base(grammar, id, expr, selectStmt)
    {
        var ON = grammar.ToTerm("ON");
        var CONFLICT = grammar.ToTerm("CONFLICT");
        var DO = grammar.ToTerm("DO");
        var NOTHING = grammar.ToTerm("NOTHING");
        var UPDATE = grammar.ToTerm("UPDATE");
        var SET = grammar.ToTerm("SET");

        var conflictColumnList = new NonTerminal("conflictColumnList");
        conflictColumnList.Rule = grammar.MakePlusRule(conflictColumnList, grammar.ToTerm(","), id);

        var conflictTarget = new NonTerminal("conflictTarget");
        conflictTarget.Rule = "(" + conflictColumnList + ")";

        var assignment = new NonTerminal("conflictAssignment");
        assignment.Rule = expr.Id + "=" + expr;

        var assignList = new NonTerminal("conflictAssignList");
        assignList.Rule = grammar.MakePlusRule(assignList, grammar.ToTerm(","), assignment);

        var conflictAction = new NonTerminal("conflictAction");
        conflictAction.Rule = DO + NOTHING | DO + UPDATE + SET + assignList;

        var onConflictOpt = new NonTerminal(OnConflictOptTermName);
        onConflictOpt.Rule = grammar.Empty | ON + CONFLICT + conflictTarget + conflictAction;

        Rule = Rule + onConflictOpt;

        grammar.MarkPunctuation("(", ")");
    }

    public override void Update(ParseTreeNode insertStmt, SqlInsertDefinition sqlInsertDefinition)
    {
        base.Update(insertStmt, sqlInsertDefinition);

        // The ON CONFLICT clause is the last child node
        var onConflictOpt = insertStmt.ChildNodes[insertStmt.ChildNodes.Count - 1];
        if (onConflictOpt.Term.Name == OnConflictOptTermName && onConflictOpt.ChildNodes.Count > 0)
        {
            var upsertClause = new SqlUpsertClause();

            // Parse conflict target columns: ON CONFLICT (col1, col2) ...
            // Child 2 is conflictTarget; its first child (after paren punctuation removal) is conflictColumnList
            var conflictTarget = onConflictOpt.ChildNodes[2];
            var conflictColumnList = conflictTarget.ChildNodes[0];
            foreach (ParseTreeNode columnNode in conflictColumnList.ChildNodes)
            {
                upsertClause.ConflictColumns.Add(Id.CreateColumn(columnNode));
            }

            // Parse conflict action: DO NOTHING | DO UPDATE SET assignList
            var conflictAction = onConflictOpt.ChildNodes[3];
            if (conflictAction.ChildNodes.Count == 2 &&
                conflictAction.ChildNodes[1].Term.Name == "NOTHING")
            {
                // DO NOTHING
                upsertClause.Action = SqlUpsertAction.DoNothing;
            }
            else
            {
                // DO UPDATE SET assignList
                upsertClause.Action = SqlUpsertAction.Update;
                var assignList = conflictAction.ChildNodes[conflictAction.ChildNodes.Count - 1];
                foreach (ParseTreeNode assignmentNode in assignList.ChildNodes)
                {
                    var sqlColumn = Id.CreateColumn(assignmentNode.ChildNodes[0]);
                    var sqlExpression = Expr.Create(assignmentNode.ChildNodes[2]);
                    upsertClause.Assignments.Add(new SqlAssignment(sqlColumn, sqlExpression));
                }
            }

            sqlInsertDefinition.UpsertClause = upsertClause;
        }
    }
}
