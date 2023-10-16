using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class ExprList : NonTerminal
{
    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// Don't forget to call Expr.Initialize()
    /// </summary>
    /// <param name="grammar"></param>
    public ExprList(Grammar grammar) : this(grammar, new Expr(grammar)) { }
    public ExprList(Grammar grammar, Id id) : this(grammar, new Expr(grammar, id)) { }

    public ExprList(Grammar grammar, Expr expr)
        : base(TermName)
    {
        Expr = expr ?? throw new ArgumentNullException(nameof(expr));

        var COMMA = grammar.ToTerm(",");

        Rule = grammar.MakePlusRule(this, COMMA, expr);
    }

    public Expr Expr { get; }

    public virtual IList<SqlExpression> Create(ParseTreeNode exprList)
    {
        if (exprList.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {exprList.Term.Name} which does not match {TermName}", nameof(exprList));
        }

        List<SqlExpression> list = new();

        foreach(ParseTreeNode expressionNode in exprList.ChildNodes)
        {
            var sqlExpression = Expr.Create(expressionNode);
            list.Add(sqlExpression);
        }

        return list;
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
