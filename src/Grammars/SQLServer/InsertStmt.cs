using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.SQLServer;

/// <summary>
/// SQL Server-specific INSERT statement that supports the OUTPUT clause.
/// OUTPUT appears between the column list and the data source (VALUES/SELECT).
/// </summary>
public class InsertStmt : SqlBuildingBlocks.InsertStmt
{
    private const string sVALUES = "VALUES";

    private readonly OutputClauseOpt outputClauseOpt;
    private readonly ExprList ssExprList;

    public InsertStmt(Grammar grammar, Id id, Expr expr, SqlBuildingBlocks.SelectStmt selectStmt,
                      OutputClauseOpt outputClauseOpt)
        : base(grammar, id, expr, selectStmt)
    {
        this.outputClauseOpt = outputClauseOpt;

        // Rebuild the rule to include OUTPUT between column list and data source.
        // Base Rule: INSERT + intoOpt + id + idlistPar + insertData
        // New Rule:  INSERT + intoOpt + id + idlistPar + outputClauseOpt + insertData
        var INSERT = grammar.ToTerm("INSERT");
        var INTO = grammar.ToTerm("INTO");
        var VALUES = grammar.ToTerm(sVALUES);

        var ssIntoOpt = new NonTerminal("ssIntoOpt");
        ssIntoOpt.Rule = grammar.Empty | INTO;

        IdList idList = new(grammar, id);
        var ssIdlistPar = new NonTerminal("ssIdlistPar");
        ssIdlistPar.Rule = "(" + idList + ")";

        ssExprList = new(grammar, expr);
        var ssValueTuple = new NonTerminal("ssValueTuple");
        ssValueTuple.Rule = "(" + ssExprList + ")";
        var ssValueTupleList = new NonTerminal("ssValueTupleList");
        ssValueTupleList.Rule = grammar.MakePlusRule(ssValueTupleList, grammar.ToTerm(","), ssValueTuple);
        var ssInsertData = new NonTerminal("ssInsertData");
        ssInsertData.Rule = selectStmt | VALUES + ssValueTupleList;

        Rule = INSERT + ssIntoOpt + id + ssIdlistPar + outputClauseOpt + ssInsertData;

        grammar.MarkPunctuation("(", ")");
    }

    public override void Update(ParseTreeNode insertStmt, SqlInsertDefinition sqlInsertDefinition)
    {
        // Our rule: INSERT + intoOpt + id + idlistPar + outputClauseOpt + insertData
        // Indices:   [0]      [1]     [2]   [3]         [4]               [5]
        //
        // Remove outputClauseOpt so base sees: INSERT + intoOpt + id + idlistPar + insertData
        var outputNode = insertStmt.ChildNodes[4];
        insertStmt.ChildNodes.RemoveAt(4);
        try
        {
            base.Update(insertStmt, sqlInsertDefinition);
        }
        finally
        {
            insertStmt.ChildNodes.Insert(4, outputNode);
        }

        // Process OUTPUT clause
        sqlInsertDefinition.OutputClause = outputClauseOpt.Create(outputNode);
    }
}
