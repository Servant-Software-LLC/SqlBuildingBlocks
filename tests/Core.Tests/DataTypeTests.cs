using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class DataTypeTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar()
        {
            DataType dataType = new(this);

            Root = dataType;
        }

        public virtual SqlDataType Create(ParseTreeNode expression) => ((DataType)Root).Create(expression);
    }

    [Fact]
    public void Integer_ZeroParameters_Allowed()
    {
        //Setup
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "INTEGER");

        //Act
        var dataType = grammar.Create(node);

        //Assert
        Assert.NotNull(dataType);

    }
}