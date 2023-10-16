using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using System.Reflection;

namespace SqlBuildingBlocks;

public class OrderByList : NonTerminal
{
    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public OrderByList(Grammar grammar) : this(grammar, new Id(grammar)) { }

    public OrderByList(Grammar grammar, Id id)
        : base(TermName)
    {
        var COMMA = grammar.ToTerm(",");

        var orderByDirOpt = new NonTerminal("orderDirOpt");
        orderByDirOpt.Rule = grammar.Empty | "ASC" | "DESC";

        var orderByMember = new NonTerminal("orderByMember");
        orderByMember.Rule = id + orderByDirOpt;

        Rule = grammar.MakePlusRule(this, COMMA, orderByMember);
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
