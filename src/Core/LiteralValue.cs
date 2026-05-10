using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System;
using System.Reflection;

namespace SqlBuildingBlocks;

public class LiteralValue : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
    private const string TrueLiteral = "TrueLiteral";
    private const string FalseLiteral = "FalseLiteral";

    public LiteralValue(Grammar grammar)
        : base(TermName)
    {
        var NULL = grammar.ToTerm("NULL");  // explicitly define NULL as a literal term

        // Define boolean literals
        var trueLiteral = new NonTerminal(TrueLiteral);
        var falseLiteral = new NonTerminal(FalseLiteral);

        StringLiteral string_literal = new("string", "'", StringOptions.AllowsDoubledQuote);
        NumberLiteral number = new("number")
        {
            // SQL numeric literals are exact, not approximate. Configure Irony's NumberLiteral
            // to materialize unsuffixed decimal literals (e.g. `3.00`) as System.Decimal rather
            // than the Irony default of System.Double. This preserves precision and matches the
            // CLR type used for typed decimal columns (#184).
            DefaultFloatType = TypeCode.Decimal
        };

        // Rule for true literals
        //trueLiteral.Rule = grammar.ToTerm("TRUE") | "True" | "T" | "t" | "yes" | "on";
        trueLiteral.Rule = grammar.ToTerm("TRUE") | "T" | "yes" | "on";
        if (grammar.CaseSensitive)
        {
            trueLiteral.Rule |= "True";
            trueLiteral.Rule |= "t";
        }

        // Rule for false literals
        //falseLiteral.Rule = grammar.ToTerm("FALSE") | "False" | "F" | "f" | "no" | "off";
        falseLiteral.Rule = grammar.ToTerm("FALSE") | "F" | "no" | "off";
        if (grammar.CaseSensitive)
        {
            falseLiteral.Rule |= "False";
            falseLiteral.Rule |= "f";
        }

        Rule = string_literal | number | trueLiteral | falseLiteral | NULL;

        grammar.MarkReservedWords("NULL");
        grammar.MarkReservedWords("TRUE", "T", "yes", "on", "FALSE", "F", "no", "off");
        if (grammar.CaseSensitive)
        {
            grammar.MarkReservedWords("True", "t", "False", "f");
        }
    }

    public virtual SqlLiteralValue Create(ParseTreeNode parseTreeNode)
    {
        if (parseTreeNode.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {parseTreeNode.Term.Name} which does not match {TermName}", nameof(parseTreeNode));
        }

        var childNode = parseTreeNode.ChildNodes[0];
        var termName = childNode.Term.Name;
        if (termName == "NULL")
            return new();

        if (termName == TrueLiteral)
            return new(true);
        if (termName == FalseLiteral)
            return new(false);

        var literalValue = childNode.Token.Value;

        // Accept every numeric runtime type the grammar (Irony NumberLiteral) can produce.
        // With DefaultFloatType = Decimal (set above), unsuffixed fractional literals produce
        // decimal and scientific-notation literals (e.g. `3.0e2`) still produce double, so we
        // accept double and float defensively in case a consumer's grammar or hand-built parse
        // tree presents one of those types. (#184)
        switch (literalValue)
        {
            case string sValue:
                return new(sValue);
            case int iValue:
                return new(iValue);
            case decimal decValue:
                return new(decValue);
            case double dValue:
                return new(dValue);
            case float fValue:
                return new(fValue);
        }

        throw new Exception($"Value provided to {nameof(SqlLiteralValue)} wasn't a recognized System.Type.  Type: {literalValue.GetType().FullName}");
    }

}
