using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.PostgreSQL;

/// <summary>
/// PostgreSQL-specific UPDATE statement that supports the full RETURNING clause.
/// RETURNING appears after the WHERE clause.
/// </summary>
public class UpdateStmt : SqlBuildingBlocks.UpdateStmt
{
    private const string UpdateSourceOptTermName = "pgUpdateSourceOpt";

    private readonly ReturningClauseOpt pgReturningClauseOpt;

    public UpdateStmt(Grammar grammar, SqlBuildingBlocks.Expr expr, FuncCall funcCall,
                      SqlBuildingBlocks.TableName tableName, WhereClauseOpt whereClauseOpt,
                      SqlBuildingBlocks.ReturningClauseOpt returningClauseOpt,
                      JoinChainOpt joinChainOpt, ReturningClauseOpt pgReturningClauseOpt)
        : base(grammar, expr, funcCall, tableName, whereClauseOpt, returningClauseOpt, joinChainOpt)
    {
        this.pgReturningClauseOpt = pgReturningClauseOpt;

        // Replace the base rule to use PostgreSQL RETURNING instead of the base one.
        // Base Rule: UPDATE + tableName + joinChainOpt + SET + assignList + updateSourceOpt + whereClauseOpt + returningClauseOpt
        // New Rule:  UPDATE + tableName + joinChainOpt + SET + assignList + updateSourceOpt + whereClauseOpt + pgReturningClauseOpt
        var UPDATE = grammar.ToTerm("UPDATE");
        var SET = grammar.ToTerm("SET");
        var COMMA = grammar.ToTerm(",");
        var FROM = grammar.ToTerm("FROM");

        var pgAssignment = new NonTerminal("pgUpdateAssignment");
        pgAssignment.Rule = expr.Id + "=" + expr;

        var pgAssignList = new NonTerminal("pgUpdateAssignList");
        pgAssignList.Rule = grammar.MakePlusRule(pgAssignList, COMMA, pgAssignment);

        var pgUpdateSourceOpt = new NonTerminal(UpdateSourceOptTermName);
        pgUpdateSourceOpt.Rule = grammar.Empty | FROM + tableName + joinChainOpt;

        Rule = UPDATE + tableName + joinChainOpt + SET + pgAssignList + pgUpdateSourceOpt + whereClauseOpt + pgReturningClauseOpt;
    }

    public override void Update(ParseTreeNode updateStmt, SqlUpdateDefinition sqlUpdateDefinition)
    {
        // Our rule: UPDATE + tableName + joinChainOpt + SET + assignList + updateSourceOpt + whereClauseOpt + pgReturningClauseOpt
        // Indices:   [0]     [1]         [2]           [3]   [4]          [5]               [6]               [7]

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

        AddSource(sqlUpdateDefinition, updateStmt.ChildNodes[5]);

        sqlUpdateDefinition.WhereClause = WhereClauseOpt.Create(updateStmt.ChildNodes[6]);

        // Process the PostgreSQL RETURNING clause
        sqlUpdateDefinition.ReturningClause = pgReturningClauseOpt.Create(updateStmt.ChildNodes[7]);
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
