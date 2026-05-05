using Irony.Parsing;
using SqlBuildingBlocks;
using SqlBuildingBlocks.Grammars.MySQL;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.IntegrationTests.Infrastructure;

/// <summary>
/// A MySQL grammar wrapper that exposes SELECT (with LIMIT/OFFSET) as the root.
/// </summary>
public sealed class MySqlGrammar : Grammar
{
    public MySqlGrammar()
    {
        Grammars.MySQL.SimpleId simpleId = new(this);
        Id id = new(this, simpleId);
        Grammars.MySQL.Expr expr = new(this, id);
        TableName tableName = new(this, id);

        Grammars.MySQL.SelectStmt selectStmt = new(this, id, expr, tableName);

        selectStmt.Expr.InitializeRule(selectStmt, selectStmt.FuncCall);
        expr.AddIntervalSupport(this);

        Root = selectStmt;
    }

    public SqlSelectDefinition CreateSelect(ParseTreeNode node, IDatabaseConnectionProvider connectionProvider, ITableSchemaProvider schemaProvider) =>
        ((Grammars.MySQL.SelectStmt)Root).Create(node, connectionProvider, schemaProvider);
}
