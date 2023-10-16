using Irony.Parsing;

namespace SqlBuildingBlocks.Grammars.AnsiSQL;

public class SelectStmt : SqlBuildingBlocks.SelectStmt
{
    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public SelectStmt(Grammar grammar) : base(grammar)
    {
        var fetchFirstOpt = new FetchFirstOpt(grammar);
        Rule += fetchFirstOpt;
    }

    public SelectStmt(Grammar grammar, Id id) : base(grammar, id)
    {
        var fetchFirstOpt = new FetchFirstOpt(grammar);
        Rule += fetchFirstOpt;
    }

    public SelectStmt(Grammar grammar, Id id, Expr expr, AliasOpt aliasOpt, TableName tableName, 
                      JoinChainOpt joinChainOpt, OrderByList orderByList, WhereClauseOpt whereClauseOpt, FuncCall funcCall)
        : base(grammar, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall)
    {
        var fetchFirstOpt = new FetchFirstOpt(grammar);
        Rule += fetchFirstOpt;
    }
}
