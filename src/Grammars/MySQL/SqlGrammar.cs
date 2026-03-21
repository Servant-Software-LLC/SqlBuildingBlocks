using Irony.Parsing;

namespace SqlBuildingBlocks.Grammars.MySQL;

/// <summary>
/// Full MySQL grammar with support for backtick-quoted identifiers, LIMIT/OFFSET,
/// ON DUPLICATE KEY UPDATE, INTERVAL expressions, and WITH ROLLUP.
/// </summary>
public class SqlGrammar : Grammar
{
    public SqlGrammar() : base(false)  //SQL is case insensitive
    {
        Comment.Register(this);

        //MySQL has special naming rules for identifiers.  (Note: the backtick)
        //REF: https://dev.mysql.com/doc/refman/8.0/en/identifiers.html
        MySQL.SimpleId simpleId = new(this);

        AliasOpt aliasOpt = new(this, simpleId);
        Id id = new(this, simpleId);
        LiteralValue literalValue = new(this);
        TableName tableName = new(this, aliasOpt, id);
        Parameter parameter = new(this);
        MySQL.Expr expr = new(this, id, literalValue, parameter);
        FuncCall funcCall = new(this, id, expr);
        JoinChainOpt joinChainOpt = new(this, tableName, expr);
        WhereClauseOpt whereClauseOpt = new(this, expr);
        OrderByList orderByList = new(this, id);
        MySQL.SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

        expr.InitializeRule(selectStmt, funcCall);
        expr.AddIntervalSupport(this);

        MySQL.InsertStmt insertStmt = new(this, id, expr, selectStmt);
        UpdateStmt updateStmt = new(this, tableName, funcCall, whereClauseOpt, joinChainOpt);
        DeleteStmt deleteStmt = new(this, tableName, whereClauseOpt, updateStmt.ReturningClauseOpt, joinChainOpt);

        Stmt stmt = new(this, selectStmt, insertStmt, updateStmt, deleteStmt);
        StmtLine stmtLine = new(this, stmt);

        Root = stmtLine;
    }
}
