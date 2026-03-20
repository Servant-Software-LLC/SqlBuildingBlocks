using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks.Grammars.PostgreSQL;

/// <summary>
/// PostgreSQL-specific RETURNING clause that supports the full syntax:
/// RETURNING *, RETURNING col1, col2, RETURNING expr AS alias.
/// </summary>
public class ReturningClauseOpt : NonTerminal
{
    private const string ReturningItemTermName = "pgReturningItem";

    private readonly SqlBuildingBlocks.Expr expr;
    private readonly AliasOpt aliasOpt;

    public ReturningClauseOpt(Grammar grammar, SqlBuildingBlocks.Expr expr, AliasOpt aliasOpt)
        : base(TermName)
    {
        this.expr = expr ?? throw new ArgumentNullException(nameof(expr));
        this.aliasOpt = aliasOpt ?? throw new ArgumentNullException(nameof(aliasOpt));

        var RETURNING = grammar.ToTerm("RETURNING");
        var STAR = grammar.ToTerm("*");

        var returningItem = new NonTerminal(ReturningItemTermName);
        returningItem.Rule = STAR | expr + aliasOpt;

        var returningList = new NonTerminal("pgReturningList");
        returningList.Rule = grammar.MakePlusRule(returningList, grammar.ToTerm(","), returningItem);

        Rule = grammar.Empty | RETURNING + returningList;

        grammar.MarkPunctuation(RETURNING);
    }

    public SqlReturningClause? Create(ParseTreeNode returningClauseOpt)
    {
        if (returningClauseOpt.ChildNodes.Count == 0)
            return null;

        var result = new SqlReturningClause();

        // Child 0 is returningList (RETURNING is punctuated away)
        var returningList = returningClauseOpt.ChildNodes[0];
        foreach (ParseTreeNode itemNode in returningList.ChildNodes)
        {
            result.Items.Add(CreateReturningItem(itemNode));
        }

        return result;
    }

    private SqlReturningItem CreateReturningItem(ParseTreeNode itemNode)
    {
        // Check for wildcard (*)
        if (itemNode.ChildNodes.Count == 1 &&
            itemNode.ChildNodes[0].Token != null &&
            itemNode.ChildNodes[0].Token.ValueString == "*")
        {
            return new SqlReturningItem();
        }

        // expr + aliasOpt
        var exprNode = itemNode.ChildNodes[0];
        var expression = expr.Create(exprNode);

        string? alias = null;
        if (itemNode.ChildNodes.Count > 1)
        {
            var aliasOptNode = itemNode.ChildNodes[1];
            if (aliasOptNode.ChildNodes.Count > 0)
            {
                alias = aliasOpt.Create(aliasOptNode);
            }
        }

        return new SqlReturningItem(expression, alias);
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
