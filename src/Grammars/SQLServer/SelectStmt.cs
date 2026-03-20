using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.SQLServer;

public class SelectStmt : SqlBuildingBlocks.SelectStmt
{
    private TopClauseOpt? topClauseOpt;
    private OptionClauseOpt? optionClauseOpt;

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor.
    /// </summary>
    /// <param name="grammar"></param>
    public SelectStmt(Grammar grammar) : base(grammar)
    {
        AddTopSupport(grammar);
        AddOptionSupport(grammar);
    }

    public SelectStmt(Grammar grammar, Id id) : base(grammar, id)
    {
        AddTopSupport(grammar);
        AddOptionSupport(grammar);
    }

    public SelectStmt(Grammar grammar, Id id, SqlBuildingBlocks.Expr expr, SqlBuildingBlocks.TableName tableName)
        : base(grammar, id, expr, tableName)
    {
        AddTopSupport(grammar);
        AddOptionSupport(grammar);
    }

    public SelectStmt(Grammar grammar, Id id, Expr expr, AliasOpt aliasOpt, SqlBuildingBlocks.TableName tableName,
                      JoinChainOpt joinChainOpt, OrderByList orderByList, WhereClauseOpt whereClauseOpt, FuncCall funcCall)
        : base(grammar, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall)
    {
        AddTopSupport(grammar);
        AddOptionSupport(grammar);
    }

    public override void Update(ParseTreeNode selectStmt, SqlSelectDefinition sqlSelectDefinition)
    {
        // With OPTION clause, selectStmt children are:
        // [0] = withClauseOpt, [1] = selectCore, [2] = setOperationListOpt,
        // [3] = orderClauseOpt, [4] = optionClauseOpt
        //
        // Remove optionClauseOpt so base sees the original 4-child structure.
        ParseTreeNode? optionNode = null;
        if (selectStmt.ChildNodes.Count > 4)
        {
            optionNode = selectStmt.ChildNodes[4];
            selectStmt.ChildNodes.RemoveAt(4);
        }

        try
        {
            base.Update(selectStmt, sqlSelectDefinition);
        }
        finally
        {
            if (optionNode != null)
                selectStmt.ChildNodes.Insert(4, optionNode);
        }

        // Process OPTION clause
        if (optionNode != null)
        {
            var hints = optionClauseOpt!.Create(optionNode);
            if (hints != null)
            {
                foreach (var hint in hints)
                    sqlSelectDefinition.QueryHints.Add(hint);
            }
        }
    }

    protected override void UpdateSelectCore(ParseTreeNode selectCore, SqlSelectDefinition sqlSelectDefinition)
    {
        // selectCore children with TOP:
        // [0] = SELECT, [1] = selRestrOpt, [2] = topClauseOpt, [3] = selList,
        // [4] = intoClauseOpt, [5] = fromClauseOpt, [6] = WhereClauseOpt,
        // [7] = GroupClauseOpt, [8] = havingClauseOpt
        //
        // We need to extract the TOP clause before delegating to base.
        // The base expects: [0]=SELECT, [1]=selRestrOpt, [2]=selList, [3]=intoClauseOpt, etc.
        // So we remove the topClauseOpt node, let base process, then restore.

        var topNode = selectCore.ChildNodes[2];
        selectCore.ChildNodes.RemoveAt(2);
        try
        {
            base.UpdateSelectCore(selectCore, sqlSelectDefinition);
        }
        finally
        {
            selectCore.ChildNodes.Insert(2, topNode);
        }

        sqlSelectDefinition.Top = topClauseOpt!.Create(topNode);
    }

    private void AddTopSupport(Grammar grammar)
    {
        topClauseOpt = new TopClauseOpt(grammar);

        // Rebuild selectCore rule to include topClauseOpt between selRestrOpt and selList:
        // SELECT + selRestrOpt + topClauseOpt + selList + intoClauseOpt + fromClauseOpt + WhereClauseOpt + GroupClauseOpt + havingClauseOpt
        var SELECT = grammar.ToTerm("SELECT");
        SelectCoreNonTerminal.Rule = SELECT + SelRestrOpt + topClauseOpt + SelList + IntoClauseOpt + FromClauseOpt +
                                     WhereClauseOpt + GroupClauseOpt + HavingClauseOpt;
    }

    private void AddOptionSupport(Grammar grammar)
    {
        optionClauseOpt = new OptionClauseOpt(grammar);

        // Append optionClauseOpt to the outer select statement rule.
        // Base Rule: withClauseOpt + selectCore + setOperationListOpt + orderClauseOpt
        // New Rule:  withClauseOpt + selectCore + setOperationListOpt + orderClauseOpt + optionClauseOpt
        Rule = Rule + optionClauseOpt;
    }
}
