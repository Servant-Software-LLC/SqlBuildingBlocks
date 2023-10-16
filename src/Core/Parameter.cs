using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class Parameter : NonTerminal
{
    private const string sNamedParameter = "NamedParameter";
    private const string sPositionalParameter = "PositionalParameter";

    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public Parameter(Grammar grammar)
        : base(TermName)
    {
        var AMPERSAND = grammar.ToTerm("@");
        var parameterName = new IdentifierTerminal("parameterName");
        
        var namedParameter = new NonTerminal(sNamedParameter);
        namedParameter.Rule = AMPERSAND + parameterName;

        var positionalParameter = new NonTerminal(sPositionalParameter);
        positionalParameter.Rule = grammar.ToTerm("?");

        Rule = namedParameter | positionalParameter;
    }

    public virtual SqlParameter Create(ParseTreeNode parameterNode)
    {
        if (parameterNode.Term.Name != TermName)
        {
            throw new ArgumentException($"Cannot create SqlParameter from node of type {parameterNode.Term.Name}");
        }

        var paramType = parameterNode.ChildNodes[0].Term.Name;
        var type = paramType == sNamedParameter ? SqlParameter.ParameterType.Named :
                   paramType == sPositionalParameter ? SqlParameter.ParameterType.Positional :
                   throw new ArgumentOutOfRangeException(nameof(paramType), $"The Parameter type {paramType} was neither {sNamedParameter} or {sPositionalParameter}");

        return new SqlParameter
        (
            type == SqlParameter.ParameterType.Named ? parameterNode.ChildNodes[0].ChildNodes[1].Token.ValueString : string.Empty
        );
    }
}
