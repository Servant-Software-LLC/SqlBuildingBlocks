using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class TransactionStmt : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public TransactionStmt(Grammar grammar)
        : base(TermName)
    {
        var BEGIN = grammar.ToTerm("BEGIN");
        var START = grammar.ToTerm("START");
        var COMMIT = grammar.ToTerm("COMMIT");
        var ROLLBACK = grammar.ToTerm("ROLLBACK");
        var TRANSACTION = grammar.ToTerm("TRANSACTION");
        var ISOLATION = grammar.ToTerm("ISOLATION");
        var LEVEL = grammar.ToTerm("LEVEL");
        var READ = grammar.ToTerm("READ");
        var COMMITTED = grammar.ToTerm("COMMITTED");
        var UNCOMMITTED = grammar.ToTerm("UNCOMMITTED");
        var REPEATABLE = grammar.ToTerm("REPEATABLE");
        var SERIALIZABLE = grammar.ToTerm("SERIALIZABLE");

        var transactionOpt = new NonTerminal("transactionOpt");
        transactionOpt.Rule = grammar.Empty | TRANSACTION;

        var isolationLevelValue = new NonTerminal("isolationLevelValue");
        isolationLevelValue.Rule = READ + COMMITTED | READ + UNCOMMITTED | REPEATABLE + READ | SERIALIZABLE;

        var isolationLevelOpt = new NonTerminal("isolationLevelOpt");
        isolationLevelOpt.Rule = grammar.Empty | ISOLATION + LEVEL + isolationLevelValue;

        var beginStmt = new NonTerminal("beginStmt");
        beginStmt.Rule = BEGIN + transactionOpt + isolationLevelOpt | START + TRANSACTION + isolationLevelOpt;

        var commitStmt = new NonTerminal("commitStmt");
        commitStmt.Rule = COMMIT + transactionOpt;

        var rollbackStmt = new NonTerminal("rollbackStmt");
        rollbackStmt.Rule = ROLLBACK + transactionOpt;

        Rule = beginStmt | commitStmt | rollbackStmt;

        grammar.MarkPunctuation(TRANSACTION);
    }

    public virtual SqlTransactionDefinition Create(ParseTreeNode transactionStmt)
    {
        SqlTransactionDefinition definition = new();
        Update(transactionStmt, definition);
        return definition;
    }

    public virtual void Update(ParseTreeNode transactionStmt, SqlTransactionDefinition definition)
    {
        if (transactionStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {transactionStmt.Term.Name} which does not match {TermName}", nameof(transactionStmt));
        }

        // The transient Stmt should resolve to one of the sub-rules
        var innerNode = transactionStmt.ChildNodes[0];
        var termName = innerNode.Term.Name;

        if (termName == "beginStmt")
        {
            definition.Kind = SqlTransactionKind.Begin;

            // Check for isolation level: beginStmt children are:
            //   BEGIN/START (keyword, may or may not be punctuated)
            //   transactionOpt (punctuated away)
            //   isolationLevelOpt
            // Find the isolationLevelOpt node
            foreach (var child in innerNode.ChildNodes)
            {
                if (child.Term.Name == "isolationLevelOpt" && child.ChildNodes.Count > 0)
                {
                    // isolationLevelOpt -> ISOLATION LEVEL isolationLevelValue
                    var isolationValueNode = child.ChildNodes.Count >= 3 ? child.ChildNodes[2] : null;
                    if (isolationValueNode != null && isolationValueNode.Term.Name == "isolationLevelValue")
                    {
                        definition.IsolationLevel = string.Join(" ",
                            isolationValueNode.ChildNodes.Select(n => n.Token?.Text?.ToUpperInvariant() ?? n.Term.Name.ToUpperInvariant()));
                    }
                }
            }
        }
        else if (termName == "commitStmt")
        {
            definition.Kind = SqlTransactionKind.Commit;
        }
        else if (termName == "rollbackStmt")
        {
            definition.Kind = SqlTransactionKind.Rollback;
        }
    }
}
