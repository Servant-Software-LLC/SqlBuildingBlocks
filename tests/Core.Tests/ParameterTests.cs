using Irony.Parsing;
using SqlBuildingBlocks.Core.Tests.Utils;
using SqlBuildingBlocks.LogicalEntities;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

public class ParameterTests
{
    private class TestGrammar : Grammar
    {
        public TestGrammar() : base(caseSensitive: false)
        {
            var parameter = new Parameter(this);

            Root = parameter;
        }

        public virtual SqlParameter Create(ParseTreeNode parameterNode) => ((Parameter)Root).Create(parameterNode);
    }

    [Fact]
    public void CanCreateNamedParameter()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "@paramName");

        var parameter = grammar.Create(node);

        Assert.Equal(SqlParameter.ParameterType.Named, parameter.Type);
        Assert.Equal("paramName", parameter.Name);
    }

    [Fact]
    public void CanCreatePositionalParameter()
    {
        TestGrammar grammar = new();
        var node = GrammarParser.Parse(grammar, "?");

        var parameter = grammar.Create(node);

        Assert.Equal(SqlParameter.ParameterType.Positional, parameter.Type);
        Assert.Equal(string.Empty, parameter.Name);
    }

}
