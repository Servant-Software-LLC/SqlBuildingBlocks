using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using System.Reflection;

namespace SqlBuildingBlocks.Grammars.AnsiSQL;

public class FetchFirstOpt : NonTerminal
{
    public FetchFirstOpt(Grammar grammar)
        : base(TermName)
    {
        var FETCH = grammar.ToTerm("FETCH");
        var FIRST = grammar.ToTerm("FIRST");
        var ROWS = grammar.ToTerm("ROWS");
        var ONLY = grammar.ToTerm("ONLY");

        var number = new NumberLiteral("number");

        Rule = grammar.Empty
            | FETCH + FIRST + number + ROWS + ONLY;

        grammar.MarkReservedWords("FETCH", "FIRST", "ROWS", "ONLY");
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

}
