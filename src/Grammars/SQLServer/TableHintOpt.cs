using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using System.Reflection;

namespace SqlBuildingBlocks.Grammars.SQLServer;

public class TableHintOpt : NonTerminal
{
    public TableHintOpt(Grammar grammar)
        : base(TermName)
    {
        var WITH = grammar.ToTerm("WITH");

        var hintName = new NonTerminal("tableHintName");
        hintName.Rule = grammar.ToTerm("NOLOCK") | "ROWLOCK" | "UPDLOCK" | "HOLDLOCK"
                      | "TABLOCK" | "TABLOCKX" | "PAGLOCK" | "READUNCOMMITTED"
                      | "READCOMMITTED" | "READPAST" | "REPEATABLEREAD"
                      | "SERIALIZABLE" | "SNAPSHOT" | "XLOCK";

        var COMMA = grammar.ToTerm(",");

        var hintList = new NonTerminal("tableHintList");
        hintList.Rule = grammar.MakePlusRule(hintList, COMMA, hintName);

        Rule = grammar.Empty
            | WITH + "(" + hintList + ")";

        grammar.MarkTransient(hintName);
        grammar.MarkReservedWords("NOLOCK", "ROWLOCK", "UPDLOCK", "HOLDLOCK",
            "TABLOCK", "TABLOCKX", "PAGLOCK", "READUNCOMMITTED",
            "READCOMMITTED", "READPAST", "REPEATABLEREAD",
            "SERIALIZABLE", "SNAPSHOT", "XLOCK");
    }

    public IList<string>? Create(ParseTreeNode tableHintOpt)
    {
        if (tableHintOpt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {tableHintOpt.Term.Name} which does not match {TermName}", nameof(tableHintOpt));
        }

        // Empty rule matched — no table hints
        if (tableHintOpt.ChildNodes.Count == 0)
            return null;

        // WITH is marked as punctuation in the base SelectStmt grammar, and ( ) are punctuation.
        // Remaining child: [hintList]
        var hintListNode = tableHintOpt.ChildNodes[0];

        var result = new List<string>();
        foreach (var hintNode in hintListNode.ChildNodes)
        {
            // After MarkTransient on hintName, the token is the hint keyword directly
            result.Add(hintNode.Token.Text.ToUpperInvariant());
        }

        return result;
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
