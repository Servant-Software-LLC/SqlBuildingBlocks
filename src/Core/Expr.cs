using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

/// <summary>
/// NOTE: Due to a cycle between <see cref="Expr"/> and <see cref="SelectStmt"/> the <see cref="InitializeRule" method must be called on this class prior to parsing./>
/// </summary>
public class Expr : NonTerminal
{
    private const string binExprTermName = "binExpr";

    private readonly Grammar grammar;
    private FuncCall? funcCall;

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// Don't forget to call <see cref="InitializeRule"/>
    /// </summary>
    /// <param name="grammar"></param>
    public Expr(Grammar grammar) : this(grammar, new Id(grammar)) { }
    public Expr(Grammar grammar, Id id) : this(grammar, id, new LiteralValue(grammar), new Parameter(grammar)) { }

    /// <summary>
    /// Don't forget to call <see cref="InitializeRule"/>
    /// </summary>
    public Expr(Grammar grammar, Id id, LiteralValue literalValue, Parameter parameter)
        : base(TermName)
    {
        this.grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
        Id = id ?? throw new ArgumentNullException(nameof(id));
        LiteralValue = literalValue ?? throw new ArgumentNullException(nameof(literalValue));
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));

        //Rule to be defined in InitializeRule() method below.
    }

    public Id Id { get; }
    public LiteralValue LiteralValue { get; }
    public Parameter Parameter { get; }


    /// <summary>
    /// Necessary to set the Rule here instead of in the ctor, due to a cycle in the definition of the Rule
    /// </summary>
    public void InitializeRule(SelectStmt selectStmt, FuncCall funcCall)
    {
        this.funcCall = funcCall ?? throw new ArgumentNullException(nameof(funcCall));

        var NOT = grammar.ToTerm("NOT");
        var PLUS = grammar.ToTerm("+");
        var MINUS = grammar.ToTerm("-");
        var MULTIPLY = grammar.ToTerm("*");
        var DIVIDE = grammar.ToTerm("/");
        var MOD = grammar.ToTerm("%");
        var BITWISE_AND = grammar.ToTerm("&");
        var BITWISE_OR = grammar.ToTerm("|");
        var BITWISE_XOR = grammar.ToTerm("^");
        var EQUAL = grammar.ToTerm("=");
        var GREATER_THAN = grammar.ToTerm(">");
        var GREATER_THAN_EQUAL = grammar.ToTerm(">=");
        var NOT_GREATER_THAN = grammar.ToTerm("!>");
        var LESS_THAN = grammar.ToTerm("<");
        var LESS_THAN_EQUAL = grammar.ToTerm("<=");
        var NOT_LESS_THAN = grammar.ToTerm("!<");
        var NOT_EQUAL_TO = grammar.ToTerm("<>");
        var NOT_EQUAL_TO_EXCL = grammar.ToTerm("!=");
        var AND = grammar.ToTerm("AND");
        var OR = grammar.ToTerm("OR");
        var LIKE = grammar.ToTerm("LIKE");
        var IN = grammar.ToTerm("IN");

        // Define operator precedence and associativity

        grammar.RegisterOperators(10, Associativity.Left, MULTIPLY, DIVIDE, MOD);
        grammar.RegisterOperators(9, Associativity.Left, PLUS, MINUS);
        grammar.RegisterOperators(8, Associativity.Left, EQUAL, GREATER_THAN, LESS_THAN, GREATER_THAN_EQUAL, LESS_THAN_EQUAL, NOT_EQUAL_TO, NOT_EQUAL_TO_EXCL, NOT_LESS_THAN, NOT_GREATER_THAN, LIKE, IN);
        grammar.RegisterOperators(7, Associativity.Left, BITWISE_AND, BITWISE_OR, BITWISE_XOR);
        grammar.RegisterOperators(6, Associativity.Left, NOT);
        grammar.RegisterOperators(5, Associativity.Left, AND);
        grammar.RegisterOperators(4, Associativity.Left, OR);

        ExprList exprList = new(grammar, this);

        var tuple = new NonTerminal("tuple");
        tuple.Rule = "(" + exprList + ")";

        var parSelectStmt = new NonTerminal("parSelectStmt");
        parSelectStmt.Rule = "(" + selectStmt + ")";

        var term = new NonTerminal("term");

        //The LiteralValue must come before Id, so that NULL doesn't get interpreted as a column name (i.e. as an Id).
        term.Rule = LiteralValue | Id | Parameter | funcCall | tuple | parSelectStmt;// | inStmt;

        var unOp = new NonTerminal("unOp");
        unOp.Rule = NOT | "+" | "-" | "~";

        var unExpr = new NonTerminal("unExpr");
        unExpr.Rule = unOp + term;

        var binOp = new NonTerminal("binOp");
        binOp.Rule = PLUS | MINUS | MULTIPLY | DIVIDE | MOD     //arithmetic
                    | BITWISE_AND | BITWISE_OR | BITWISE_XOR    //bit
                    | EQUAL | GREATER_THAN | LESS_THAN | GREATER_THAN_EQUAL | LESS_THAN_EQUAL | NOT_EQUAL_TO | NOT_EQUAL_TO_EXCL | NOT_LESS_THAN | NOT_GREATER_THAN
                    | AND | OR | LIKE | NOT + LIKE | IN | NOT + IN;

        var binExpr = new NonTerminal(binExprTermName);
        binExpr.Rule = this + binOp + this;

        Rule = term | unExpr | binExpr;

        //Note: we cannot declare binOp as transient because it includes operators "NOT LIKE", "NOT IN" consisting of two tokens. 
        // Transient non-terminals cannot have more than one non-punctuation child nodes.
        // Instead, we set flag InheritPrecedence on binOp , so that it inherits precedence value from it's children, and this precedence is used
        // in conflict resolution when binOp node is sitting on the stack
        grammar.MarkTransient(this /*expression*/, term, unOp);
        binOp.SetFlag(TermFlags.InheritPrecedence);
    }

    public virtual SqlExpression Create(ParseTreeNode expression)
    {
        var nodeTermName = expression.Term.Name;

        //Is this node a binary expression?
        if (nodeTermName == binExprTermName)
        {
            return new(CreateBinaryExpression(expression));
        }

        //Is this node a column reference?
        if (nodeTermName == Id.TermName)
        {
            return new(Id!.CreateColumnRef(expression));
        }

        //Is this a parameter?
        if (nodeTermName == Parameter.TermName)
        {
            return new(Parameter!.Create(expression));
        }

        //Is this a function call?
        if (nodeTermName == FuncCall.TermName)
        {
            return new(funcCall!.Create(expression));
        }

        //Is this is literal value (string, int, double, etc.)
        if (nodeTermName == LiteralValue.TermName)
        {
            return new(LiteralValue!.Create(expression));
        }

        throw new ArgumentException($"The {nameof(SqlExpression)} class does not know how to parse the {nameof(ParseTreeNode)} provided to its ctor");
    }

    protected internal SqlBinaryExpression CreateBinaryExpression(ParseTreeNode binExpr)
    {
        if (binExpr.Term.Name != binExprTermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {binExpr.Term.Name} which does not match {binExprTermName}", nameof(binExpr));
        }

        var left = Create(binExpr.ChildNodes[0]);
        var operatorSymbol = binExpr.ChildNodes[1].ChildNodes[0].Token.Text;
        var right = Create(binExpr.ChildNodes[2]);

        return new(left, CreateOperator(operatorSymbol), right);
    }

    internal static SqlBinaryOperator CreateOperator(string sBinaryOperator) =>
        sBinaryOperator.ToUpper() switch
        {
            "=" => SqlBinaryOperator.Equal,
            "<" => SqlBinaryOperator.LessThan,
            "<=" => SqlBinaryOperator.LessThanEqual,
            ">" => SqlBinaryOperator.GreaterThan,
            ">=" => SqlBinaryOperator.GreaterThanEqual,
            "AND" => SqlBinaryOperator.And,
            "OR" => SqlBinaryOperator.Or,
            "LIKE" => SqlBinaryOperator.Like,
            _ => throw new ArgumentException($"Invalid binary operator {sBinaryOperator}", nameof(sBinaryOperator))
        };

    internal static string CreateOperator(SqlBinaryOperator binaryOperator) =>
        binaryOperator switch
        {
            SqlBinaryOperator.Equal => "=",
            SqlBinaryOperator.LessThan => "<",
            SqlBinaryOperator.LessThanEqual => "<=",
            SqlBinaryOperator.GreaterThan => ">",
            SqlBinaryOperator.GreaterThanEqual => ">=",
            SqlBinaryOperator.And => "AND",
            SqlBinaryOperator.Or => "OR",
            SqlBinaryOperator.Like => "LIKE",
            _ => throw new ArgumentException($"Invalid binary operator {binaryOperator}", nameof(binaryOperator))
        };


    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
