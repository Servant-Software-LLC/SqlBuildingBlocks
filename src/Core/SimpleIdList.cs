using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using System.Reflection;

namespace SqlBuildingBlocks;

public class SimpleIdList : NonTerminal
{
    private SimpleId simpleId;

    public SimpleIdList(SimpleId simpleId, Grammar grammar)
        : base(TermName)
    {
        this.simpleId = simpleId ?? throw new ArgumentNullException(nameof(simpleId));

        var COMMA = grammar.ToTerm(",");

        Rule = grammar.MakePlusRule(this, COMMA, simpleId);
    }

    public IEnumerable<string> Create(ParseTreeNode simpleIdList) =>
        simpleIdList.ChildNodes.Create(simpleId.Create);

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
