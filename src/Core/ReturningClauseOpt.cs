using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class ReturningClauseOpt : NonTerminal
{
    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public ReturningClauseOpt(Grammar grammar, Id id)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));

        var NUMBER = new NumberLiteral("number");

        var RETURNING = grammar.ToTerm("RETURNING");
        var returningValue = new NonTerminal("returningValue");
        returningValue.Rule = id | NUMBER;

        Rule = RETURNING + returningValue | grammar.Empty;

        grammar.MarkPunctuation(RETURNING);
        grammar.MarkTransient(returningValue);
    }

    public Id Id { get; }

    public virtual SqlReturning Create(ParseTreeNode returningClause)
    {
        if (returningClause.ChildNodes.Count > 0)
        {
            var returningValue = returningClause.ChildNodes[0];
            if (returningValue.Term.Name == Id.TermName)
            {
                var returningColumn = Id.CreateColumn(returningValue);
                return new(returningColumn);
            }
            else
            {
                var returningInt = (int)returningValue.Token.Value;
                return new(returningInt);
            }
        }

        return null;
    }
}
