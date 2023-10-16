using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks.Shared;

/// <summary>
/// PostgreSQL and SQLite share this same behavior (MySQL is a bit different.  <see cref="SqlBuildingBlocks.Grammars.MySQL.LimitOffsetClauseOpt"/>)
/// </summary>
public class LimitOffsetClauseOpt : NonTerminal
{
    protected readonly KeyTerm LIMIT;
    protected readonly KeyTerm OFFSET;
    protected readonly NonTerminal value;

    public LimitOffsetClauseOpt(Grammar grammar) : this(grammar, new Parameter(grammar)) { }
    public LimitOffsetClauseOpt(Grammar grammar, Parameter parameter)
        : base(TermName)
    {
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));

        LIMIT = grammar.ToTerm("LIMIT");
        OFFSET = grammar.ToTerm("OFFSET");
        NumberLiteral number = new("number");

        value = new NonTerminal("value");
        value.Rule = number | parameter;

        // Define the optional limit and offset clause
        // Note that the limit and offset values are defined as number literals
        // Limit and Offset can be specified alone or together

        Rule = grammar.Empty
            | LIMIT + value
            | OFFSET + value
            | LIMIT + value + OFFSET + value;

        grammar.MarkTransient(value);
    }

    public Parameter Parameter { get; }

    public virtual SqlLimitOffset Create(ParseTreeNode limitOffsetClauseOpt)
    {
        if (limitOffsetClauseOpt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {limitOffsetClauseOpt.Term.Name} which does not match {TermName}", nameof(limitOffsetClauseOpt));
        }

        SqlLimitOffset result = new SqlLimitOffset();
        for (int i = 1; i < limitOffsetClauseOpt.ChildNodes.Count; i++)
        {
            ParseTreeNode childNode = limitOffsetClauseOpt.ChildNodes[i];

            if (limitOffsetClauseOpt.ChildNodes[i - 1].Term.Name == "LIMIT")
            {
                result.RowCount = GetValue(childNode);
                continue;
            }
            else if (limitOffsetClauseOpt.ChildNodes[i - 1].Term.Name == "OFFSET")
            {
                result.RowOffset = GetValue(childNode);
                continue;
            }
        }

        return result;
    }

    protected SqlLimitValue GetValue(ParseTreeNode parseTreeNode)
    {
        if (parseTreeNode.Term.Name == "number")
            return new(Convert.ToInt32(parseTreeNode.Token.Value));

        if (parseTreeNode.Term.Name == Parameter.TermName)
            return new(Parameter.Create(parseTreeNode));

        throw new ArgumentException($"Cannot create SqlLimitValue from node of type {parseTreeNode.Term.Name}.  Expected either a number or parameter here.");
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
