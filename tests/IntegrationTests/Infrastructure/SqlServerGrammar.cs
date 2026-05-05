using Irony.Parsing;
using SqlBuildingBlocks;
using SqlBuildingBlocks.Grammars.SQLServer;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.IntegrationTests.Infrastructure;

/// <summary>
/// A SQL Server grammar wrapper that exposes SELECT (with TOP support) as the root.
/// </summary>
public sealed class SqlServerGrammar : Grammar
{
    public SqlServerGrammar() : base(false)
    {
        Grammars.SQLServer.SimpleId simpleId = new(this);
        AliasOpt aliasOpt = new(this, simpleId);
        Id id = new(this, simpleId);
        LiteralValue literalValue = new(this);
        TableHintOpt tableHintOpt = new(this);
        Grammars.SQLServer.TableName tableName = new(this, aliasOpt, id, tableHintOpt);
        Parameter parameter = new(this);
        SqlBuildingBlocks.Expr expr = new(this, id, literalValue, parameter);
        FuncCall funcCall = new(this, id, expr);
        JoinChainOpt joinChainOpt = new(this, tableName, expr);
        WhereClauseOpt whereClauseOpt = new(this, expr);
        OrderByList orderByList = new(this, id);
        Grammars.SQLServer.SelectStmt selectStmt =
            new(this, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall);

        expr.InitializeRule(selectStmt, funcCall);

        Root = selectStmt;
    }

    public SqlSelectDefinition CreateSelect(ParseTreeNode node, IDatabaseConnectionProvider connectionProvider, ITableSchemaProvider schemaProvider) =>
        ((Grammars.SQLServer.SelectStmt)Root).Create(node, connectionProvider, schemaProvider);
}
