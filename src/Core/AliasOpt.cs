using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using System.Reflection;

namespace SqlBuildingBlocks;


public class AliasOpt : NonTerminal
{
    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public AliasOpt(Grammar grammar) : this(grammar, new SimpleId(grammar)) { }

    public AliasOpt(Grammar grammar, SimpleId id_simple)
        : base(nameof(AliasOpt).CamelCase())
    {
        var AS = grammar.ToTerm("AS");

        var asOpt = new NonTerminal("asOpt");
        asOpt.Rule = grammar.Empty | AS;


        grammar.MarkPunctuation(AS);
        grammar.MarkTransient(asOpt);

        Rule = grammar.Empty | asOpt + id_simple; // use id_simple for alias
    }

    //TODO: Add a Create() method that would be called by TableName and SelectStmt.
    public string Create(ParseTreeNode aliasId)
    {
        if (aliasId.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {aliasId.Term.Name} which does not match {TermName}", nameof(aliasId));
        }

        return aliasId.ChildNodes[0].ChildNodes[0].Token.ValueString;
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
