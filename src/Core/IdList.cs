using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using System.Reflection;

namespace SqlBuildingBlocks;

public class IdList : NonTerminal
{
    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public IdList(Grammar grammar) : this(grammar, new Id(grammar)) { }

    public IdList(Grammar grammar, Id id)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));

        var COMMA = grammar.ToTerm(",");

        Rule = grammar.MakePlusRule(this, COMMA, id);
    }

    public Id Id { get; }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
