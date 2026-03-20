using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using System.Reflection;

namespace SqlBuildingBlocks.Grammars.SQLServer;

public class OptionClauseOpt : NonTerminal
{
    private const string sQueryHint = "queryHint";

    public OptionClauseOpt(Grammar grammar)
        : base(TermName)
    {
        var OPTION = grammar.ToTerm("OPTION");
        var COMMA = grammar.ToTerm(",");
        var number = new NumberLiteral("optionNumber");

        var queryHint = new NonTerminal(sQueryHint);
        queryHint.Rule = grammar.ToTerm("RECOMPILE")
                       | grammar.ToTerm("MAXDOP") + number
                       | grammar.ToTerm("FAST") + number
                       | grammar.ToTerm("LOOP") + "JOIN"
                       | grammar.ToTerm("HASH") + "JOIN"
                       | grammar.ToTerm("MERGE") + "JOIN"
                       | grammar.ToTerm("LOOP") + "UNION"
                       | grammar.ToTerm("HASH") + "UNION"
                       | grammar.ToTerm("MERGE") + "UNION"
                       | grammar.ToTerm("CONCAT") + "UNION"
                       | grammar.ToTerm("HASH") + "GROUP"
                       | grammar.ToTerm("ORDER") + "GROUP"
                       | grammar.ToTerm("OPTIMIZE") + "FOR" + "UNKNOWN"
                       | grammar.ToTerm("EXPAND") + "VIEWS"
                       | grammar.ToTerm("FORCE") + "ORDER";

        var queryHintList = new NonTerminal("queryHintList");
        queryHintList.Rule = grammar.MakePlusRule(queryHintList, COMMA, queryHint);

        Rule = grammar.Empty
            | OPTION + "(" + queryHintList + ")";

        grammar.MarkPunctuation(OPTION);
        grammar.MarkReservedWords("OPTION", "RECOMPILE", "MAXDOP", "FAST",
            "CONCAT", "EXPAND", "VIEWS", "FORCE", "OPTIMIZE");
    }

    public IList<string>? Create(ParseTreeNode optionClauseOpt)
    {
        if (optionClauseOpt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {optionClauseOpt.Term.Name} which does not match {TermName}", nameof(optionClauseOpt));
        }

        // Empty rule matched — no OPTION clause
        if (optionClauseOpt.ChildNodes.Count == 0)
            return null;

        // OPTION is punctuation, ( ) are punctuation.
        // Remaining child: [queryHintList]
        var queryHintListNode = optionClauseOpt.ChildNodes[0];

        var result = new List<string>();
        foreach (var hintNode in queryHintListNode.ChildNodes)
        {
            // Each queryHint has one or more child tokens — join them as the hint string
            var parts = new List<string>();
            foreach (var child in hintNode.ChildNodes)
            {
                parts.Add(child.Token.Text.ToUpperInvariant());
            }
            result.Add(string.Join(" ", parts));
        }

        return result;
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
