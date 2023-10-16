using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class WhereClauseOpt : NonTerminal
{
    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// Don't forget to call Expr.Initialize()
    /// </summary>
    /// <param name="grammar"></param>
    public WhereClauseOpt(Grammar grammar) : this(grammar, new Expr(grammar)) { }
    public WhereClauseOpt(Grammar grammar, Id id) : this(grammar, new Expr(grammar, id)) { }

    public WhereClauseOpt(Grammar grammar, Expr expr)
        : base(TermName)
    {
        Expr = expr ?? throw new ArgumentNullException(nameof(expr));

        Rule = grammar.Empty | "WHERE" + expr;
    }

    public Expr Expr { get; }

    public virtual SqlBinaryExpression? Create(ParseTreeNode whereClause)
    {
        if (whereClause.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {whereClause.Term.Name} which does not match {TermName}", nameof(whereClause));
        }

        if (whereClause.ChildNodes.Count == 0)
            return null;

        return Expr.CreateBinaryExpression(whereClause.ChildNodes[1]);
    }

}
