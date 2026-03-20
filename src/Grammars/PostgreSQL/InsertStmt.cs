using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.PostgreSQL;

/// <summary>
/// PostgreSQL-specific INSERT statement that supports ON CONFLICT DO UPDATE SET ... / DO NOTHING
/// and the RETURNING clause.
/// Supports conflict targets: column list, ON CONSTRAINT, or no target.
/// Supports optional WHERE clauses on both the conflict target and DO UPDATE action.
/// </summary>
public class InsertStmt : SqlBuildingBlocks.InsertStmt
{
    private const string OnConflictOptTermName = "onConflictOpt";
    private const string ConflictTargetTermName = "conflictTarget";
    private const string ConstraintTargetTermName = "constraintTarget";
    private const string ConflictActionTermName = "conflictAction";
    private const string ConflictTargetWhereOptTermName = "conflictTargetWhereOpt";
    private const string ConflictUpdateWhereOptTermName = "conflictUpdateWhereOpt";

    private readonly ReturningClauseOpt? pgReturningClauseOpt;

    public InsertStmt(Grammar grammar, Id id, SqlBuildingBlocks.Expr expr, SqlBuildingBlocks.SelectStmt selectStmt)
        : this(grammar, id, expr, selectStmt, null)
    {
    }

    public InsertStmt(Grammar grammar, Id id, SqlBuildingBlocks.Expr expr, SqlBuildingBlocks.SelectStmt selectStmt,
                      ReturningClauseOpt? returningClauseOpt)
        : base(grammar, id, expr, selectStmt)
    {
        pgReturningClauseOpt = returningClauseOpt;

        var ON = grammar.ToTerm("ON");
        var CONFLICT = grammar.ToTerm("CONFLICT");
        var CONSTRAINT = grammar.ToTerm("CONSTRAINT");
        var DO = grammar.ToTerm("DO");
        var NOTHING = grammar.ToTerm("NOTHING");
        var UPDATE = grammar.ToTerm("UPDATE");
        var SET = grammar.ToTerm("SET");
        var WHERE = grammar.ToTerm("WHERE");

        var conflictColumnList = new NonTerminal("conflictColumnList");
        conflictColumnList.Rule = grammar.MakePlusRule(conflictColumnList, grammar.ToTerm(","), id);

        // Column-based conflict target: (col1, col2)
        var columnTarget = new NonTerminal("columnTarget");
        columnTarget.Rule = "(" + conflictColumnList + ")";

        // Optional WHERE on conflict target (partial index filter)
        var conflictTargetWhereOpt = new NonTerminal(ConflictTargetWhereOptTermName);
        conflictTargetWhereOpt.Rule = grammar.Empty | WHERE + expr;

        // Constraint-based conflict target: ON CONSTRAINT constraint_name
        var constraintTarget = new NonTerminal(ConstraintTargetTermName);
        constraintTarget.Rule = ON + CONSTRAINT + id;

        // Conflict target: column list (with optional WHERE), constraint name, or empty
        var conflictTarget = new NonTerminal(ConflictTargetTermName);
        conflictTarget.Rule = columnTarget + conflictTargetWhereOpt | constraintTarget | grammar.Empty;

        var assignment = new NonTerminal("conflictAssignment");
        assignment.Rule = expr.Id + "=" + expr;

        var assignList = new NonTerminal("conflictAssignList");
        assignList.Rule = grammar.MakePlusRule(assignList, grammar.ToTerm(","), assignment);

        // Optional WHERE on DO UPDATE
        var conflictUpdateWhereOpt = new NonTerminal(ConflictUpdateWhereOptTermName);
        conflictUpdateWhereOpt.Rule = grammar.Empty | WHERE + expr;

        var conflictAction = new NonTerminal(ConflictActionTermName);
        conflictAction.Rule = DO + NOTHING | DO + UPDATE + SET + assignList + conflictUpdateWhereOpt;

        var onConflictOpt = new NonTerminal(OnConflictOptTermName);
        onConflictOpt.Rule = grammar.Empty | ON + CONFLICT + conflictTarget + conflictAction;

        if (returningClauseOpt != null)
            Rule = Rule + onConflictOpt + returningClauseOpt;
        else
            Rule = Rule + onConflictOpt;

        grammar.MarkPunctuation("(", ")");
    }

    public override void Update(ParseTreeNode insertStmt, SqlInsertDefinition sqlInsertDefinition)
    {
        base.Update(insertStmt, sqlInsertDefinition);

        // Find the ON CONFLICT and RETURNING clauses at the end
        // The last children are: ... + onConflictOpt [+ returningClauseOpt]
        int lastIndex = insertStmt.ChildNodes.Count - 1;

        if (pgReturningClauseOpt != null)
        {
            // returningClauseOpt is last, onConflictOpt is second to last
            var returningNode = insertStmt.ChildNodes[lastIndex];
            var onConflictOpt = insertStmt.ChildNodes[lastIndex - 1];

            sqlInsertDefinition.ReturningClause = pgReturningClauseOpt.Create(returningNode);

            if (onConflictOpt.Term.Name == OnConflictOptTermName && onConflictOpt.ChildNodes.Count > 0)
            {
                ParseOnConflict(onConflictOpt, sqlInsertDefinition);
            }
        }
        else
        {
            // No RETURNING, onConflictOpt is last
            var onConflictOpt = insertStmt.ChildNodes[lastIndex];
            if (onConflictOpt.Term.Name == OnConflictOptTermName && onConflictOpt.ChildNodes.Count > 0)
            {
                ParseOnConflict(onConflictOpt, sqlInsertDefinition);
            }
        }
    }

    private void ParseOnConflict(ParseTreeNode onConflictOpt, SqlInsertDefinition sqlInsertDefinition)
    {
        var upsertClause = new SqlUpsertClause();

        // Child layout: ON CONFLICT conflictTarget conflictAction
        var conflictTarget = onConflictOpt.ChildNodes[2];
        ParseConflictTarget(conflictTarget, upsertClause);

        var conflictAction = onConflictOpt.ChildNodes[3];
        ParseConflictAction(conflictAction, upsertClause);

        sqlInsertDefinition.UpsertClause = upsertClause;
    }

    private void ParseConflictTarget(ParseTreeNode conflictTarget, SqlUpsertClause upsertClause)
    {
        if (conflictTarget.Term.Name != ConflictTargetTermName)
            return;

        // Empty target (ON CONFLICT DO ...)
        if (conflictTarget.ChildNodes.Count == 0)
            return;

        var firstChild = conflictTarget.ChildNodes[0];

        // ON CONSTRAINT constraint_name
        if (firstChild.Term.Name == ConstraintTargetTermName)
        {
            // constraintTarget children: ON CONSTRAINT id
            // After MarkPunctuation for ON and CONSTRAINT keywords,
            // the id is the last child
            var constraintNameNode = firstChild.ChildNodes[firstChild.ChildNodes.Count - 1];
            upsertClause.ConstraintName = Id.CreateColumn(constraintNameNode).ColumnName;
            return;
        }

        // Column-based target: (col1, col2) [WHERE ...]
        // firstChild is columnTarget, whose first child is conflictColumnList
        var conflictColumnList = firstChild.ChildNodes[0];
        foreach (ParseTreeNode columnNode in conflictColumnList.ChildNodes)
        {
            upsertClause.ConflictColumns.Add(Id.CreateColumn(columnNode));
        }

        // Check for optional WHERE on conflict target
        if (conflictTarget.ChildNodes.Count > 1)
        {
            var whereOpt = conflictTarget.ChildNodes[1];
            if (whereOpt.Term.Name == ConflictTargetWhereOptTermName && whereOpt.ChildNodes.Count > 0)
            {
                upsertClause.ConflictTargetWhereCondition = Expr.Create(whereOpt.ChildNodes[1]);
            }
        }
    }

    private void ParseConflictAction(ParseTreeNode conflictAction, SqlUpsertClause upsertClause)
    {
        if (conflictAction.Term.Name != ConflictActionTermName)
            return;

        // Check if DO NOTHING
        if (conflictAction.ChildNodes.Count >= 2 &&
            conflictAction.ChildNodes[1].Term.Name == "NOTHING")
        {
            upsertClause.Action = SqlUpsertAction.DoNothing;
            return;
        }

        // DO UPDATE SET assignList [WHERE ...]
        upsertClause.Action = SqlUpsertAction.Update;

        // Find the assignList (after DO UPDATE SET)
        var assignList = conflictAction.ChildNodes[3];
        foreach (ParseTreeNode assignmentNode in assignList.ChildNodes)
        {
            var sqlColumn = Id.CreateColumn(assignmentNode.ChildNodes[0]);
            var sqlExpression = Expr.Create(assignmentNode.ChildNodes[2]);
            upsertClause.Assignments.Add(new SqlAssignment(sqlColumn, sqlExpression));
        }

        // Check for optional WHERE on DO UPDATE
        var whereOpt = conflictAction.ChildNodes[conflictAction.ChildNodes.Count - 1];
        if (whereOpt.Term.Name == ConflictUpdateWhereOptTermName && whereOpt.ChildNodes.Count > 0)
        {
            upsertClause.WhereCondition = Expr.Create(whereOpt.ChildNodes[1]);
        }
    }
}
