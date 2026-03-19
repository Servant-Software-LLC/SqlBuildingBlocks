using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class RenameTableTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            SimpleId simpleId = new(this);
            Id id = new(this, simpleId);
            RenameTableStmt renameTableStmt = new(this, id);

            Root = renameTableStmt;
        }

        public SqlRenameTableDefinition Create(ParseTreeNode renameTableStmt) =>
            ((RenameTableStmt)Root).Create(renameTableStmt);
    }

    [Fact]
    public void AlterTable_RenameTo_Ansi()
    {
        const string sql = @"ALTER TABLE Customers RENAME TO Clients";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal("Customers", result.SourceTable!.TableName);
        Assert.Equal("Clients", result.TargetTable!.TableName);
    }

    [Fact]
    public void RenameTable_MySql()
    {
        const string sql = @"RENAME TABLE Customers TO Clients";

        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, sql);

        var result = grammar.Create(node);

        Assert.Equal("Customers", result.SourceTable!.TableName);
        Assert.Equal("Clients", result.TargetTable!.TableName);
    }
}
