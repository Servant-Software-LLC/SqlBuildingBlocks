using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.SQLServer;

/// <summary>
/// SQL Server-specific MERGE statement that supports the OUTPUT clause.
/// MERGE [INTO] target USING source ON condition
/// WHEN MATCHED [AND cond] THEN UPDATE SET ... | DELETE
/// WHEN NOT MATCHED [BY TARGET] [AND cond] THEN INSERT (...) VALUES (...)
/// WHEN NOT MATCHED BY SOURCE [AND cond] THEN UPDATE SET ... | DELETE
/// [OUTPUT ...]
/// </summary>
public class MergeStmt : SqlBuildingBlocks.MergeStmt
{
    private readonly OutputClauseOpt outputClauseOpt;

    public MergeStmt(Grammar grammar, Id id, Expr expr, SqlBuildingBlocks.TableName tableName,
                     FuncCall funcCall, OutputClauseOpt outputClauseOpt)
        : base(grammar, id, expr, tableName, funcCall)
    {
        this.outputClauseOpt = outputClauseOpt;

        // Append OUTPUT clause to the rule.
        // Base Rule: MERGE + intoOpt + tableName + USING + tableName + ON + expr + whenClauseList
        // New Rule:  MERGE + intoOpt + tableName + USING + tableName + ON + expr + whenClauseList + outputClauseOpt
        Rule = Rule + outputClauseOpt;
    }

    public override void Update(ParseTreeNode mergeStmt, SqlMergeDefinition definition)
    {
        // Our rule: MERGE + intoOpt + tableName + USING + tableName + ON + expr + whenClauseList + outputClauseOpt
        // Indices:   [0]     [1]       [2]        [3]     [4]        [5]   [6]    [7]              [8]

        // Remove outputClauseOpt so base sees the original structure
        var lastIndex = mergeStmt.ChildNodes.Count - 1;
        var outputNode = mergeStmt.ChildNodes[lastIndex];
        mergeStmt.ChildNodes.RemoveAt(lastIndex);
        try
        {
            base.Update(mergeStmt, definition);
        }
        finally
        {
            mergeStmt.ChildNodes.Add(outputNode);
        }

        // Process OUTPUT clause
        definition.OutputClause = outputClauseOpt.Create(outputNode);
    }
}
