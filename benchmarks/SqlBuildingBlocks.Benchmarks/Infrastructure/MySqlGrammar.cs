using Irony.Parsing;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Benchmarks.Infrastructure;

/// <summary>
/// MySQL grammar wrapper that exposes SELECT (with LIMIT/OFFSET, WITH ROLLUP, INTERVAL)
/// as the root for parser benchmarks.
/// </summary>
internal sealed class MySqlGrammar : Grammar
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

    public SqlSelectDefinition CreateSelect(ParseTreeNode node) =>
        ((Grammars.MySQL.SelectStmt)Root).Create(node);
}
