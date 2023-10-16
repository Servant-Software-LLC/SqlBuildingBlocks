using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks.Grammars.MySQL;

/// <summary>
/// This version of LimitOffsetClauseOpt is different from the shared one in SqlBuildingBlocks.Core project, in that
/// it allows for the comma notation.  REF: https://www.mysqltutorial.org/mysql-limit.aspx
/// </summary>
public class LimitOffsetClauseOpt : SqlBuildingBlocks.Shared.LimitOffsetClauseOpt
{
    private readonly KeyTerm COMMA;

    public LimitOffsetClauseOpt(Grammar grammar) : this(grammar, new Parameter(grammar)) { }
    public LimitOffsetClauseOpt(Grammar grammar, Parameter parameter)
        : base(grammar, parameter)
    {
        COMMA = grammar.ToTerm(",");

        // Define the optional limit and offset clause
        // Note that the limit and offset values are defined as number literals
        // Limit can be specified alone, or with an offset following a comma or the OFFSET keyword
        // If the offset is specified with the OFFSET keyword, a limit must be provided

        Rule = grammar.Empty
            | LIMIT + value
            | LIMIT + value + COMMA + value
            | LIMIT + value + OFFSET + value;
    }

    public override SqlLimitOffset Create(ParseTreeNode limitOffsetClauseOpt)
    {
        if (limitOffsetClauseOpt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {limitOffsetClauseOpt.Term.Name} which does not match {TermName}", nameof(limitOffsetClauseOpt));
        }

        // Get the number of child nodes.
        var childNodes = limitOffsetClauseOpt.ChildNodes;
        int childNodesCount = childNodes.Count;

        if (childNodesCount == 0)
            return null;

        var sqlLimitOffset = new SqlLimitOffset();

        if (childNodesCount == 2)
        {
            sqlLimitOffset.RowCount = GetValue(childNodes[1]);
        }
        else if (childNodesCount == 4) 
        {
            var containsComma = childNodes[2].Token.KeyTerm == COMMA;
            sqlLimitOffset.RowCount = containsComma ? GetValue(childNodes[3]) : GetValue(childNodes[1]);
            sqlLimitOffset.RowOffset = containsComma ? GetValue(childNodes[1]) : GetValue(childNodes[3]);
        }

        return sqlLimitOffset;
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
