using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.MySQL;

/// <summary>
/// MySQL-specific INSERT statement that supports ON DUPLICATE KEY UPDATE,
/// including VALUES(column) references.
/// </summary>
public class InsertStmt : SqlBuildingBlocks.InsertStmt
{
    private const string OnDuplicateKeyOptTermName = "onDuplicateKeyOpt";
    private const string ValuesFuncTermName = "valuesFunc";

    public InsertStmt(Grammar grammar, Id id, Expr expr, SelectStmt selectStmt)
        : base(grammar, id, expr, selectStmt)
    {
        var ON = grammar.ToTerm("ON");
        var DUPLICATE = grammar.ToTerm("DUPLICATE");
        var KEY = grammar.ToTerm("KEY");
        var UPDATE = grammar.ToTerm("UPDATE");
        var VALUES = grammar.ToTerm("VALUES");

        // VALUES(column) function reference — MySQL-specific syntax for referring
        // to the value that would have been inserted on conflict.
        var valuesFunc = new NonTerminal(ValuesFuncTermName);
        valuesFunc.Rule = VALUES + "(" + id + ")";

        // The RHS of a duplicate-key assignment can be a regular expression
        // or a VALUES(column) reference. Marked transient so the parse tree
        // collapses directly to the matched alternative.
        var dupKeyExpr = new NonTerminal("dupKeyExpr");
        dupKeyExpr.Rule = expr | valuesFunc;
        grammar.MarkTransient(dupKeyExpr);

        var assignment = new NonTerminal("dupKeyAssignment");
        assignment.Rule = id + "=" + dupKeyExpr;

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
                var exprNode = assignmentNode.ChildNodes[2];
                var sqlExpression = CreateDupKeyExpression(exprNode);
                upsertClause.Assignments.Add(new SqlAssignment(sqlColumn, sqlExpression));
            }

            sqlInsertDefinition.UpsertClause = upsertClause;
        }
    }

    private SqlExpression CreateDupKeyExpression(ParseTreeNode node)
    {
        // Check if this is a VALUES(column) reference
        if (node.Term.Name == ValuesFuncTermName)
        {
            // Children after punctuation stripping: [VALUES_keyword, id]
            // The id node is the last child.
            var idNode = node.ChildNodes[node.ChildNodes.Count - 1];
            var columnRef = Id.CreateColumnRef(idNode);
            var argExpression = new SqlExpression(columnRef);

            var valuesFunction = new SqlFunction("VALUES");
            valuesFunction.Arguments.Add(argExpression);
            return new SqlExpression(valuesFunction);
        }

        // Regular expression — delegate to the standard Expr parser.
        return Expr.Create(node);
    }
}
