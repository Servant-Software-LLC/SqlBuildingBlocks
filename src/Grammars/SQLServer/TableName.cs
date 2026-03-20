using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.SQLServer;

public class TableName : SqlBuildingBlocks.TableName
{
    private readonly TableHintOpt tableHintOpt;

    public TableName(Grammar grammar, AliasOpt aliasOpt, Id id, TableHintOpt tableHintOpt)
        : base(grammar, aliasOpt, id)
    {
        this.tableHintOpt = tableHintOpt ?? throw new ArgumentNullException(nameof(tableHintOpt));

        // Append tableHintOpt to the existing rule for direct table references.
        // Base rule: id + spaceOpt + aliasOpt
        // New rule:  id + spaceOpt + aliasOpt + tableHintOpt
        // The derived table alternative (added later by InitializeRule) is unaffected.
        Rule = Rule + tableHintOpt;
    }

    public override SqlTable Create(ParseTreeNode tableId)
    {
        // For derived tables, delegate entirely to base
        if (tableId.ChildNodes.Count > 0 && tableId.ChildNodes[0].Term.Name == "derivedTable")
            return base.Create(tableId);

        // For direct table references, children after transient spaceOpt removal:
        // [0] = id, [1] = aliasOpt, [2] = tableHintOpt
        //
        // We temporarily remove the tableHintOpt node so base.Create sees the
        // original [id, aliasOpt] structure it expects.
        ParseTreeNode? hintNode = null;
        if (tableId.ChildNodes.Count >= 3)
        {
            hintNode = tableId.ChildNodes[2];
            tableId.ChildNodes.RemoveAt(2);
        }

        SqlTable table;
        try
        {
            table = base.Create(tableId);
        }
        finally
        {
            if (hintNode != null)
                tableId.ChildNodes.Insert(2, hintNode);
        }

        // Apply table hints
        if (hintNode != null)
        {
            var hints = tableHintOpt.Create(hintNode);
            if (hints != null)
                table.TableHints = hints;
        }

        return table;
    }
}
