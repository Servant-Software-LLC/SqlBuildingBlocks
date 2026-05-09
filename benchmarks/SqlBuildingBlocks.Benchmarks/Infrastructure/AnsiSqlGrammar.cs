using Irony.Parsing;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Benchmarks.Infrastructure;

/// <summary>
/// AnsiSQL grammar wrapper sized for benchmark use. The grammar is constructed once
/// and reused across iterations — Irony's LanguageData/Parser construction is non-trivial
/// and would dominate the measurement otherwise.
/// </summary>
internal sealed class AnsiSqlGrammar : Grammar
{
    public AnsiSqlGrammar() : base(false)
    {
        SimpleId simpleId = new(this);
        AliasOpt aliasOpt = new(this, simpleId);
        Id id = new(this, simpleId);
        LiteralValue literalValue = new(this);
        TableName tableName = new(this, aliasOpt, id);
        Parameter parameter = new(this);
        Expr expr = new(this, id, literalValue, parameter);
        FuncCall funcCall = new(this, id, expr);
        JoinChainOpt joinChainOpt = new(this, tableName, expr);
        WhereClauseOpt whereClauseOpt = new(this, expr);
        OrderByList orderByList = new(this, id);
        Grammars.AnsiSQL.SelectStmt selectStmt =
            new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

        expr.InitializeRule(selectStmt, funcCall);

        Root = selectStmt;
    }

    public SqlSelectDefinition CreateSelect(ParseTreeNode node, IDatabaseConnectionProvider connection, ITableSchemaProvider schema) =>
        ((Grammars.AnsiSQL.SelectStmt)Root).Create(node, connection, schema);

    public SqlSelectDefinition CreateSelect(ParseTreeNode node) =>
        ((Grammars.AnsiSQL.SelectStmt)Root).Create(node);
}
