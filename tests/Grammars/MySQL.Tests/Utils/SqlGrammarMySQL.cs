using Irony.Parsing;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.MySQL.Tests.Utils;

public class SqlGrammarMySQL : Grammar
{
    public SqlGrammarMySQL()
    {
        //MySQL has special naming rules for identifiers.  (Note: the backtick)
        //REF: https://dev.mysql.com/doc/refman/8.0/en/identifiers.html
        SqlBuildingBlocks.Grammars.MySQL.SimpleId simpleId = new(this);
        Id id = new(this, simpleId);
        SqlBuildingBlocks.Grammars.MySQL.Expr expr = new(this, id);
        TableName tableName = new(this, id);

        SqlBuildingBlocks.Grammars.MySQL.SelectStmt selectStmt = new(this, id, expr, tableName);

        selectStmt.Expr.InitializeRule(selectStmt, selectStmt.FuncCall);
        expr.AddIntervalSupport(this);
        Root = selectStmt;
    }

    public virtual SqlSelectDefinition Create(ParseTreeNode selectStmt, IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider, IFunctionProvider functionProvider) =>
        ((SelectStmt)Root).Create(selectStmt, databaseConnectionProvider, tableSchemaProvider, functionProvider);

}
