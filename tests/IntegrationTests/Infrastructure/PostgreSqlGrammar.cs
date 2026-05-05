using Irony.Parsing;
using SqlBuildingBlocks;
using SqlBuildingBlocks.Grammars.PostgreSQL;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.IntegrationTests.Infrastructure;

/// <summary>
/// A PostgreSQL grammar wrapper that exposes both SELECT and INSERT (with RETURNING)
/// as alternative roots, accessed via separate parsers.
/// </summary>
public sealed class PostgreSqlSelectGrammar : Grammar
{
    public PostgreSqlSelectGrammar() : base(false)
    {
        SimpleId simpleId = new(this);
        AliasOpt aliasOpt = new(this, simpleId);
        Id id = new(this, simpleId);
        LiteralValue literalValue = new(this);
        TableName tableName = new(this, aliasOpt, id);
        Parameter parameter = new(this);
        Grammars.PostgreSQL.Expr expr = new(this, id, literalValue, parameter);
        FuncCall funcCall = new(this, id, expr);
        JoinChainOpt joinChainOpt = new(this, tableName, expr);
        WhereClauseOpt whereClauseOpt = new(this, expr);
        OrderByList orderByList = new(this, id);
        SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

        expr.InitializeRule(selectStmt, funcCall);

        Root = selectStmt;
    }

    public SqlSelectDefinition CreateSelect(ParseTreeNode node, IDatabaseConnectionProvider connectionProvider, ITableSchemaProvider schemaProvider) =>
        ((SelectStmt)Root).Create(node, connectionProvider, schemaProvider);
}

/// <summary>
/// PostgreSQL grammar exposing INSERT...RETURNING as the root, used by the integration
/// test that validates RETURNING parses end-to-end (no engine execution exists for INSERT today).
/// </summary>
public sealed class PostgreSqlInsertGrammar : Grammar
{
    public PostgreSqlInsertGrammar() : base(false)
    {
        SimpleId simpleId = new(this);
        AliasOpt aliasOpt = new(this, simpleId);
        Id id = new(this, simpleId);
        LiteralValue literalValue = new(this);
        TableName tableName = new(this, aliasOpt, id);
        Parameter parameter = new(this);
        Grammars.PostgreSQL.Expr expr = new(this, id, literalValue, parameter);
        FuncCall funcCall = new(this, id, expr);
        JoinChainOpt joinChainOpt = new(this, tableName, expr);
        WhereClauseOpt whereClauseOpt = new(this, expr);
        OrderByList orderByList = new(this, id);
        SelectStmt selectStmt = new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

        expr.InitializeRule(selectStmt, funcCall);

        Grammars.PostgreSQL.ReturningClauseOpt returningClauseOpt = new(this, expr, aliasOpt);
        Grammars.PostgreSQL.InsertStmt insertStmt = new(this, id, expr, selectStmt, returningClauseOpt);

        Root = insertStmt;
    }

    public SqlInsertDefinition CreateInsert(ParseTreeNode node) =>
        ((Grammars.PostgreSQL.InsertStmt)Root).Create(node);
}
