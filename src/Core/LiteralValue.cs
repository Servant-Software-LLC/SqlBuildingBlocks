using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class LiteralValue : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public LiteralValue(Grammar grammar)
        : base(TermName)
    {
        var NULL = grammar.ToTerm("NULL");  // explicitly define NULL as a literal term

        StringLiteral string_literal = new("string", "'", StringOptions.AllowsDoubledQuote);
        NumberLiteral number = new("number");

        Rule = string_literal | number | NULL;

        grammar.MarkReservedWords("NULL");
    }

    public virtual SqlLiteralValue Create(ParseTreeNode parseTreeNode)
    {
        if (parseTreeNode.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {parseTreeNode.Term.Name} which does not match {TermName}", nameof(parseTreeNode));
        }

        var childNode = parseTreeNode.ChildNodes[0];
        if (childNode.Term.Name == "NULL")
            return new();

        var literalValue = childNode.Token.Value;

        switch (literalValue)
        {
            case string sValue:
                return new(sValue);
            case int iValue:
                return new(iValue);
        }

        throw new Exception($"Value provided to {nameof(SqlLiteralValue)} wasn't a recognized System.Type.  Type: {literalValue.GetType().FullName}");
    }

}
