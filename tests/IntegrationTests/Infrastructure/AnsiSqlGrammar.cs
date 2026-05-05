using Irony.Parsing;
using SqlBuildingBlocks;
using SqlBuildingBlocks.Grammars.AnsiSQL;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.IntegrationTests.Infrastructure;

/// <summary>
/// A complete AnsiSQL grammar suitable for parsing SELECT statements end-to-end.
/// Mirrors the pattern a consumer would assemble when wiring SqlBuildingBlocks into
/// their own query layer: build a Grammar, expose a Create() method for parse trees.
/// </summary>
public sealed class AnsiSqlGrammar : Grammar
{
    public AnsiSqlGrammar() : base(false) // SQL is case-insensitive
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

    public SqlSelectDefinition CreateSelect(ParseTreeNode node, IDatabaseConnectionProvider connectionProvider, ITableSchemaProvider schemaProvider) =>
        ((Grammars.AnsiSQL.SelectStmt)Root).Create(node, connectionProvider, schemaProvider);

    public SqlSelectDefinition CreateSelect(ParseTreeNode node) =>
        ((Grammars.AnsiSQL.SelectStmt)Root).Create(node);
}
