using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

/// <summary>
/// Covers some psedo-operators and special forms like ANY(...), SOME(...), ALL(...), EXISTS(...), IN(...)
/// </summary>
public class FuncCall : NonTerminal
{
    private readonly ExprList exprList;

    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public FuncCall(Grammar grammar) : this(grammar, new Id(grammar)) { }
    public FuncCall(Grammar grammar, Id id) : this(grammar, id, new Expr(grammar, id)) { }
    public FuncCall(Grammar grammar, Id id, Expr expr)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Expr = expr ?? throw new ArgumentNullException(nameof(expr));

        exprList = new(grammar, expr);

        var funcArgs = new NonTerminal("funcArgs");
        funcArgs.Rule = grammar.Empty | exprList;

        Rule = id + "(" + funcArgs + ")";
    }

    public Id Id { get; }
    public Expr Expr { get; }

    public virtual SqlFunction Create(ParseTreeNode node)
    {
        if (node.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {node.Term.Name} which does not match {TermName}", nameof(node));
        }

        var function = new SqlFunction
        (
            node.ChildNodes[0].ChildNodes[0].ChildNodes[0].Token.ValueString
        );

        // if there are arguments
        if (node.ChildNodes.Count > 1 && node.ChildNodes[1].ChildNodes.Count > 0)
        {
            // the arguments are in a child node of the node with the function name
            var argsNode = node.ChildNodes[1];
            var expressions = exprList.Create(argsNode.ChildNodes[0]);
            foreach (var expression in expressions)
            {
                function.Arguments.Add(expression);
            }
        }

        return function;
    }
}
