using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.Grammars.MySQL.Tests.Utils;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.QueryProcessing;
using SqlBuildingBlocks.Utils;
using System.Linq;
using Xunit;

namespace SqlBuildingBlocks.Grammars.MySQL.Tests;

public class QueryEngineTests
{
    [Fact]
    public void Results_ColumnAlias()
    {
        SqlGrammarMySQL grammar = new();
        var node = GrammarParser.Parse(grammar, @"SELECT VARIABLE_NAME Variable_name, VARIABLE_VALUE Value FROM performance_schema.session_variables WHERE VARIABLE_NAME LIKE 'sql_mode'");

        FakeDatabaseConnectionProvider databaseConnectionProvider = new();
        FakeTableDataProvider tableDataProvider = new();
        SqlSelectDefinition selectDefinition = grammar.Create(node, databaseConnectionProvider, tableDataProvider, null);

        Assert.False(selectDefinition.InvalidReferences);

        AllTableDataProvider allTableDataProvider = new(new ITableDataProvider[] { tableDataProvider });
        var queryEngine = new QueryEngine(allTableDataProvider, selectDefinition);

        var queryResults = queryEngine.Query();

        var results = queryResults.Results.ToList();

        Assert.Equal(1, results.Count);
        Assert.Equal("Variable_name", results[0].Table.Columns[0].ColumnName);
        Assert.Equal("Value", results[0].Table.Columns[1].ColumnName);
    }
}
