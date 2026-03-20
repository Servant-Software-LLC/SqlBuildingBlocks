using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class MergeStmt : NonTerminal
{
    private const string WhenMatchedTermName = "mergeWhenMatched";
    private const string WhenNotMatchedTermName = "mergeWhenNotMatched";
    private const string WhenNotMatchedBySourceTermName = "mergeWhenNotMatchedBySource";
    private const string WhenClauseTermName = "mergeWhenClause";
    private const string MergeInsertTermName = "mergeInsert";
    private const string MergeUpdateTermName = "mergeUpdate";
    private const string AdditionalConditionOptTermName = "mergeAdditionalConditionOpt";

    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public MergeStmt(Grammar grammar, Id id, Expr expr, TableName tableName, FuncCall funcCall)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Expr = expr ?? throw new ArgumentNullException(nameof(expr));
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));

        var MERGE = grammar.ToTerm("MERGE");
        var INTO = grammar.ToTerm("INTO");
        var USING = grammar.ToTerm("USING");
        var ON = grammar.ToTerm("ON");
        var WHEN = grammar.ToTerm("WHEN");
        var MATCHED = grammar.ToTerm("MATCHED");
        var NOT = grammar.ToTerm("NOT");
        var BY = grammar.ToTerm("BY");
        var SOURCE = grammar.ToTerm("SOURCE");
        var TARGET = grammar.ToTerm("TARGET");
        var AND = grammar.ToTerm("AND");
        var THEN = grammar.ToTerm("THEN");
        var UPDATE = grammar.ToTerm("UPDATE");
        var SET = grammar.ToTerm("SET");
        var DELETE = grammar.ToTerm("DELETE");
        var INSERT = grammar.ToTerm("INSERT");
        var VALUES = grammar.ToTerm("VALUES");
        var COMMA = grammar.ToTerm(",");

        var intoOpt = new NonTerminal("mergeIntoOpt");
        intoOpt.Rule = grammar.Empty | INTO;

        // Assignment list for UPDATE SET
        var assignment = new NonTerminal("mergeAssignment");
        assignment.Rule = id + "=" + expr;

        var assignList = new NonTerminal("mergeAssignList");
        assignList.Rule = grammar.MakePlusRule(assignList, COMMA, assignment);

        // Column list and values for INSERT
        IdList idList = new(grammar, id);
        var idlistPar = new NonTerminal("mergeIdlistPar");
        idlistPar.Rule = "(" + idList + ")";

        ExprList exprList = new(grammar, expr);
        var valueTuple = new NonTerminal("mergeValueTuple");
        valueTuple.Rule = "(" + exprList + ")";

        // Actions
        var mergeUpdate = new NonTerminal(MergeUpdateTermName);
        mergeUpdate.Rule = UPDATE + SET + assignList;

        var mergeDelete = new NonTerminal("mergeDelete");
        mergeDelete.Rule = DELETE;

        var mergeInsert = new NonTerminal(MergeInsertTermName);
        mergeInsert.Rule = INSERT + idlistPar + VALUES + valueTuple;

        // Additional condition: AND <condition>
        var additionalConditionOpt = new NonTerminal(AdditionalConditionOptTermName);
        additionalConditionOpt.Rule = grammar.Empty | AND + expr;

        // WHEN MATCHED [AND condition] THEN UPDATE SET ... | DELETE
        var matchedAction = new NonTerminal("mergeMatchedAction");
        matchedAction.Rule = mergeUpdate | mergeDelete;

        var whenMatched = new NonTerminal(WhenMatchedTermName);
        whenMatched.Rule = WHEN + MATCHED + additionalConditionOpt + THEN + matchedAction;

        // WHEN NOT MATCHED [BY TARGET] [AND condition] THEN INSERT ...
        var byTargetOpt = new NonTerminal("mergeByTargetOpt");
        byTargetOpt.Rule = grammar.Empty | BY + TARGET;

        var whenNotMatched = new NonTerminal(WhenNotMatchedTermName);
        whenNotMatched.Rule = WHEN + NOT + MATCHED + byTargetOpt + additionalConditionOpt + THEN + mergeInsert;

        // WHEN NOT MATCHED BY SOURCE [AND condition] THEN UPDATE SET ... | DELETE
        var notMatchedBySourceAction = new NonTerminal("mergeNotMatchedBySourceAction");
        notMatchedBySourceAction.Rule = mergeUpdate | mergeDelete;

        var whenNotMatchedBySource = new NonTerminal(WhenNotMatchedBySourceTermName);
        whenNotMatchedBySource.Rule = WHEN + NOT + MATCHED + BY + SOURCE + additionalConditionOpt + THEN + notMatchedBySourceAction;

        // When clause list (one or more WHEN clauses)
        var whenClause = new NonTerminal(WhenClauseTermName);
        whenClause.Rule = whenMatched | whenNotMatched | whenNotMatchedBySource;

        var whenClauseList = new NonTerminal("mergeWhenClauseList");
        whenClauseList.Rule = grammar.MakePlusRule(whenClauseList, whenClause);

        Rule = MERGE + intoOpt + tableName + USING + tableName + ON + expr + whenClauseList;

        grammar.MarkPunctuation("(", ")");
        grammar.MarkReservedWords("MERGE", "USING", "MATCHED", "SOURCE", "TARGET");

        ExprList = exprList;
    }

    public Id Id { get; }
    public Expr Expr { get; }
    public TableName TableName { get; }
    protected ExprList ExprList { get; }

    public virtual SqlMergeDefinition Create(ParseTreeNode mergeStmt)
    {
        SqlMergeDefinition definition = new();
        Update(mergeStmt, definition);
        return definition;
    }

    public virtual void Update(ParseTreeNode mergeStmt, SqlMergeDefinition definition)
    {
        if (mergeStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException(
                $"Cannot create building block of type {thisMethod!.ReturnType}. " +
                $"The TermName for node is {mergeStmt.Term.Name} which does not match {TermName}",
                nameof(mergeStmt));
        }

        // Rule: MERGE + intoOpt + tableName + USING + tableName + ON + expr + whenClauseList
        // After punctuation of MERGE keyword (not punctuated):
        // [0]=MERGE, [1]=intoOpt, [2]=tableName, [3]=USING, [4]=tableName, [5]=ON, [6]=expr, [7]=whenClauseList
        // But MERGE, USING, ON are keywords and may or may not be punctuated.
        // Let's index based on the actual children.

        definition.TargetTable = TableName.Create(mergeStmt.ChildNodes[2]);
        definition.SourceTable = TableName.Create(mergeStmt.ChildNodes[4]);
        definition.SearchCondition = Expr.Create(mergeStmt.ChildNodes[6]);

        var whenClauseList = mergeStmt.ChildNodes[7];
        foreach (var whenClauseNode in whenClauseList.ChildNodes)
        {
            var whenClause = CreateWhenClause(whenClauseNode);
            definition.WhenClauses.Add(whenClause);
        }
    }

    protected virtual SqlMergeWhenClause CreateWhenClause(ParseTreeNode whenClauseNode)
    {
        // whenClause wraps one of: whenMatched | whenNotMatched | whenNotMatchedBySource
        var innerNode = whenClauseNode.ChildNodes[0];
        string termName = innerNode.Term.Name;

        if (termName == WhenMatchedTermName)
            return CreateWhenMatched(innerNode);
        if (termName == WhenNotMatchedTermName)
            return CreateWhenNotMatched(innerNode);
        if (termName == WhenNotMatchedBySourceTermName)
            return CreateWhenNotMatchedBySource(innerNode);

        throw new ArgumentException($"Unexpected WHEN clause term: {termName}");
    }

    private SqlMergeWhenClause CreateWhenMatched(ParseTreeNode node)
    {
        // WHEN and THEN are stripped by Irony (marked as punctuation by CASE expression).
        // Actual children: [0]=MATCHED, [1]=additionalConditionOpt, [2]=matchedAction
        var clause = new SqlMergeWhenClause { WhenType = SqlMergeWhenType.Matched };

        clause.AdditionalCondition = CreateAdditionalCondition(node.ChildNodes[1]);

        var actionNode = node.ChildNodes[2];
        ProcessMatchedAction(clause, actionNode.ChildNodes[0]);

        return clause;
    }

    private SqlMergeWhenClause CreateWhenNotMatched(ParseTreeNode node)
    {
        // WHEN and THEN are stripped by Irony.
        // Actual children: [0]=NOT, [1]=MATCHED, [2]=byTargetOpt, [3]=additionalConditionOpt, [4]=mergeInsert
        var clause = new SqlMergeWhenClause
        {
            WhenType = SqlMergeWhenType.NotMatched,
            ActionType = SqlMergeActionType.Insert
        };

        clause.AdditionalCondition = CreateAdditionalCondition(node.ChildNodes[3]);

        var insertNode = node.ChildNodes[4];
        ProcessInsertAction(clause, insertNode);

        return clause;
    }

    private SqlMergeWhenClause CreateWhenNotMatchedBySource(ParseTreeNode node)
    {
        // WHEN and THEN are stripped by Irony.
        // Actual children: [0]=NOT, [1]=MATCHED, [2]=BY, [3]=SOURCE, [4]=additionalConditionOpt, [5]=action
        var clause = new SqlMergeWhenClause { WhenType = SqlMergeWhenType.NotMatchedBySource };

        clause.AdditionalCondition = CreateAdditionalCondition(node.ChildNodes[4]);

        var actionNode = node.ChildNodes[5];
        ProcessMatchedAction(clause, actionNode.ChildNodes[0]);

        return clause;
    }

    private SqlExpression? CreateAdditionalCondition(ParseTreeNode additionalConditionOptNode)
    {
        if (additionalConditionOptNode.Term.Name != AdditionalConditionOptTermName ||
            additionalConditionOptNode.ChildNodes.Count == 0)
            return null;

        // AND + expr => child[0]=AND, child[1]=expr
        return Expr.Create(additionalConditionOptNode.ChildNodes[1]);
    }

    private void ProcessMatchedAction(SqlMergeWhenClause clause, ParseTreeNode actionNode)
    {
        if (actionNode.Term.Name == MergeUpdateTermName)
        {
            clause.ActionType = SqlMergeActionType.Update;
            // UPDATE + SET + assignList => [0]=UPDATE, [1]=SET, [2]=assignList
            var assignList = actionNode.ChildNodes[2];
            foreach (ParseTreeNode assignment in assignList.ChildNodes)
            {
                var sqlColumn = Id.CreateColumn(assignment.ChildNodes[0]);
                var sqlExpression = Expr.Create(assignment.ChildNodes[2]);
                clause.Assignments.Add(new SqlAssignment(sqlColumn, sqlExpression));
            }
        }
        else
        {
            // DELETE
            clause.ActionType = SqlMergeActionType.Delete;
        }
    }

    protected virtual void ProcessInsertAction(SqlMergeWhenClause clause, ParseTreeNode insertNode)
    {
        // mergeInsert: INSERT + idlistPar + VALUES + valueTuple
        // [0]=INSERT, [1]=idlistPar (contains idList), [2]=VALUES, [3]=valueTuple (contains exprList)

        var idListNode = insertNode.ChildNodes[1].ChildNodes[0];
        var columns = idListNode.ChildNodes;
        foreach (var colNode in columns)
        {
            clause.InsertColumns.Add(Id.CreateColumn(colNode));
        }

        var valueTupleNode = insertNode.ChildNodes[3];
        var values = ExprList.Create(valueTupleNode.ChildNodes[0]);
        foreach (var val in values)
        {
            clause.InsertValues.Add(val);
        }
    }
}
