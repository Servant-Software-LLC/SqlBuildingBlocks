using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.MySQL;

public class SelectStmt : SqlBuildingBlocks.SelectStmt
{
    private LimitOffsetClauseOpt? limitOffsetClauseOpt;

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public SelectStmt(Grammar grammar) : base(grammar)
    {
        limitOffsetClauseOpt = new(grammar);
        Rule += limitOffsetClauseOpt;
    }

    public SelectStmt(Grammar grammar, Id id) : base(grammar, id) 
    {
        limitOffsetClauseOpt = new(grammar);
        Rule += limitOffsetClauseOpt;
    }

    public SelectStmt(Grammar grammar, Id id, Expr expr, AliasOpt aliasOpt, TableName tableName, 
                      JoinChainOpt joinChainOpt, OrderByList orderByList, WhereClauseOpt whereClauseOpt, FuncCall funcCall)
        : base(grammar, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall)
    {
        limitOffsetClauseOpt = new(grammar);
        Rule += limitOffsetClauseOpt;
    }

    public override SqlSelectDefinition Create(ParseTreeNode selectStmt)
    {
        var sqlSelectDefinition = base.Create(selectStmt);

        if (selectStmt.ChildNodes.Count > 9)
            sqlSelectDefinition.Limit = limitOffsetClauseOpt!.Create(selectStmt.ChildNodes[9]);

        return sqlSelectDefinition;
    }
}
