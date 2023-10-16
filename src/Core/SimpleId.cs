using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using System.Reflection;

namespace SqlBuildingBlocks;

public class SimpleId : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public SimpleId(Grammar grammar)
        : base(nameof(SimpleId).CamelCase())
    {
        // Define terms for brackets and quotes
        var openBracket = grammar.ToTerm("[");
        var closeBracket = grammar.ToTerm("]");

        var identifier = new IdentifierTerminal("identifer");

        // Define rule
        Rule = identifier | openBracket + identifier + closeBracket;

        grammar.MarkPunctuation(openBracket, closeBracket);
    }

    public string Create(ParseTreeNode simpleId)
    {
        if (simpleId.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {simpleId.Term.Name} which does not match {TermName}", nameof(simpleId));
        }

        return simpleId.ChildNodes[0].Token.ValueString;
    }
}