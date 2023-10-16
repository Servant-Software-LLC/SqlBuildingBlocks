using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class JoinChainOpt : NonTerminal
{
    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public JoinChainOpt(Grammar grammar) : this(grammar, new TableName(grammar), new Expr(grammar)) { }
    public JoinChainOpt(Grammar grammar, Id id) : this(grammar, new TableName(grammar, id), new Expr(grammar, id)) { }
    public JoinChainOpt(Grammar grammar, TableName tableName, Expr expr)
        : base(TermName)
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        Expr = expr ?? throw new ArgumentNullException(nameof(expr));
        
        var JOIN = grammar.ToTerm("JOIN");
        var ON = grammar.ToTerm("ON");

        var joinKindOpt = new NonTerminal("joinKindOpt");
        joinKindOpt.Rule = grammar.Empty | "INNER" | "LEFT" | "RIGHT";

        var join = new NonTerminal("join");
        join.Rule = joinKindOpt + JOIN + tableName + ON + expr;

        Rule = grammar.MakeStarRule(this, join);
    }

    public TableName TableName { get; }
    public Expr Expr { get; }

    public virtual IList<SqlJoin> Create(ParseTreeNode joinChainOptNode)
    {
        if (joinChainOptNode.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {joinChainOptNode.Term.Name} which does not match {TermName}", nameof(joinChainOptNode));
        }

        List<SqlJoin> joinChain = new();

        foreach(var joinNode in joinChainOptNode.ChildNodes)
        {
            joinChain.Add(CreateJoin(joinNode));
        }

        return joinChain;
    }

    private SqlJoin CreateJoin(ParseTreeNode joinNode)
    {

        //JOIN table
        var tableNameNode = joinNode.ChildNodes[2];
        var table = TableName.Create(tableNameNode);

        //JOIN ON
        var condition = Expr.CreateBinaryExpression(joinNode.ChildNodes[4]);

        SqlJoin join = new(table, condition);

        //JOIN type
        var joinKindOpt = joinNode.ChildNodes[0];
        if (joinKindOpt.ChildNodes.Count > 0)
        {
            var joinChainOptText = joinKindOpt.ChildNodes[0].Token.Text;
            join.JoinKind = (SqlJoinKind)Enum.Parse(typeof(SqlJoinKind), joinChainOptText, true);
        }

        return join;
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
