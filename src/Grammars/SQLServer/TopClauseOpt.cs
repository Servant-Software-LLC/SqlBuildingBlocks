using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks.Grammars.SQLServer;

public class TopClauseOpt : NonTerminal
{
    private readonly Parameter? parameter;

    public TopClauseOpt(Grammar grammar) : this(grammar, new Parameter(grammar)) { }

    public TopClauseOpt(Grammar grammar, Parameter parameter)
        : base(TermName)
    {
        this.parameter = parameter;

        var TOP = grammar.ToTerm("TOP");
        var PERCENT = grammar.ToTerm("PERCENT");
        var WITH = grammar.ToTerm("WITH");
        var TIES = grammar.ToTerm("TIES");

        var number = new NumberLiteral("number");

        // TOP value can be a bare number or a parenthesized expression
        var topValue = new NonTerminal("topValue");
        topValue.Rule = number | parameter | "(" + number + ")" | "(" + parameter + ")";

        var percentOpt = new NonTerminal("percentOpt");
        percentOpt.Rule = grammar.Empty | PERCENT;

        var withTiesOpt = new NonTerminal("withTiesOpt");
        withTiesOpt.Rule = grammar.Empty | WITH + TIES;

        Rule = grammar.Empty
            | TOP + topValue + percentOpt + withTiesOpt;

        grammar.MarkTransient(topValue);
        grammar.MarkPunctuation(TOP);
        grammar.MarkReservedWords("TOP", "PERCENT", "TIES");
    }

    public SqlTopClause? Create(ParseTreeNode topClauseOpt)
    {
        if (topClauseOpt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {topClauseOpt.Term.Name} which does not match {TermName}", nameof(topClauseOpt));
        }

        // Empty rule matched — no TOP clause
        if (topClauseOpt.ChildNodes.Count == 0)
            return null;

        var result = new SqlTopClause();

        // Child 0 is the value (number or parameter, after transient stripping)
        var valueNode = topClauseOpt.ChildNodes[0];
        result.Count = GetValue(valueNode);

        // Child 1 is percentOpt
        if (topClauseOpt.ChildNodes.Count > 1)
        {
            var percentOptNode = topClauseOpt.ChildNodes[1];
            result.Percent = percentOptNode.ChildNodes.Count > 0;
        }

        // Child 2 is withTiesOpt
        if (topClauseOpt.ChildNodes.Count > 2)
        {
            var withTiesOptNode = topClauseOpt.ChildNodes[2];
            result.WithTies = withTiesOptNode.ChildNodes.Count > 0;
        }

        return result;
    }

    private SqlLimitValue GetValue(ParseTreeNode parseTreeNode)
    {
        if (parseTreeNode.Term.Name == "number")
            return new(Convert.ToInt32(parseTreeNode.Token.Value));

        if (parameter != null && parseTreeNode.Term.Name == Parameter.TermName)
            return new(parameter.Create(parseTreeNode));

        throw new ArgumentException($"Cannot create SqlLimitValue from node of type {parseTreeNode.Term.Name}.  Expected either a number or parameter here.");
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
