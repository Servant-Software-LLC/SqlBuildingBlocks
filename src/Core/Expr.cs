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
    private const string isNullExprTermName = "isNullExpr";
    private const string betweenExprTermName = "betweenExpr";

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
        var IS = grammar.ToTerm("IS");
        var NULL = grammar.ToTerm("NULL");
        var BETWEEN = grammar.ToTerm("BETWEEN");

        // Define operator precedence and associativity

        grammar.RegisterOperators(10, Associativity.Left, MULTIPLY, DIVIDE, MOD);
        grammar.RegisterOperators(9, Associativity.Left, PLUS, MINUS);
        grammar.RegisterOperators(8, Associativity.Left, EQUAL, GREATER_THAN, LESS_THAN, GREATER_THAN_EQUAL, LESS_THAN_EQUAL, NOT_EQUAL_TO, NOT_EQUAL_TO_EXCL, NOT_LESS_THAN, NOT_GREATER_THAN, LIKE, IN, BETWEEN);
        grammar.RegisterOperators(7, Associativity.Left, BITWISE_AND, BITWISE_OR, BITWISE_XOR);
        // IS has same precedence as comparison operators; registering it resolves the shift-reduce conflict
        // for the isNullExpr postfix production (expr IS NULL / expr IS NOT NULL).
        grammar.RegisterOperators(7, Associativity.Left, IS);
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

        // IS NULL / IS NOT NULL — unary postfix predicates
        var isNullExpr = new NonTerminal(isNullExprTermName);
        isNullExpr.Rule = this + IS + NULL | this + IS + NOT + NULL;

        // BETWEEN / NOT BETWEEN — ternary range predicates
        // betweenBound restricts the lower/upper bound to arithmetic-level expressions so that the
        // AND keyword cannot be consumed as a logical-AND binary operator within the bound, which
        // would create an unresolvable shift-reduce conflict with the logical AND operator.
        var betweenArithOp = new NonTerminal("betweenArithOp");
        betweenArithOp.Rule = PLUS | MINUS | MULTIPLY | DIVIDE | MOD
                             | BITWISE_AND | BITWISE_OR | BITWISE_XOR
                             | EQUAL | GREATER_THAN | LESS_THAN | GREATER_THAN_EQUAL | LESS_THAN_EQUAL
                             | NOT_EQUAL_TO | NOT_EQUAL_TO_EXCL | NOT_LESS_THAN | NOT_GREATER_THAN
                             | LIKE | IN;

        var betweenBound = new NonTerminal("betweenBound");
        betweenBound.Rule = term
                          | unExpr
                          | betweenBound + betweenArithOp + betweenBound;

        var betweenExpr = new NonTerminal(betweenExprTermName);
        betweenExpr.Rule = this + BETWEEN + betweenBound + AND + betweenBound
                         | this + NOT + BETWEEN + betweenBound + AND + betweenBound;

        betweenArithOp.SetFlag(TermFlags.InheritPrecedence);

        Rule = term | unExpr | binExpr | isNullExpr | betweenExpr;

        //Note: we cannot declare binOp as transient because it includes operators "NOT LIKE", "NOT IN" consisting of two tokens.
        // Transient non-terminals cannot have more than one non-punctuation child nodes.
        // Instead, we set flag InheritPrecedence on binOp , so that it inherits precedence value from it's children, and this precedence is used
        // in conflict resolution when binOp node is sitting on the stack
        grammar.MarkTransient(this /*expression*/, term, unOp);
        binOp.SetFlag(TermFlags.InheritPrecedence);

        grammar.MarkReservedWords("IS", "BETWEEN");
    }

    public virtual SqlExpression Create(ParseTreeNode expression)
    {
        var nodeTermName = expression.Term.Name;

        //Is this node a binary expression?
        if (nodeTermName == binExprTermName)
        {
            return new(CreateBinaryExpression(expression));
        }

        //Is this an IS NULL / IS NOT NULL expression?
        if (nodeTermName == isNullExprTermName)
        {
            return new(CreateIsNullExpression(expression));
        }

        //Is this a BETWEEN / NOT BETWEEN expression?
        if (nodeTermName == betweenExprTermName)
        {
            return new(CreateBetweenExpression(expression));
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

        // Is this a betweenBound node (a bound expression inside BETWEEN)?
        if (nodeTermName == "betweenBound")
        {
            if (expression.ChildNodes.Count == 1)
            {
                // Single child (term or unExpr) — delegate to the child
                return Create(expression.ChildNodes[0]);
            }

            // 3 children: betweenBound betweenArithOp betweenBound
            var left = Create(expression.ChildNodes[0]);
            var operatorSymbol = expression.ChildNodes[1].ChildNodes[0].Token.Text;
            var right = Create(expression.ChildNodes[2]);
            return new(new SqlBinaryExpression(left, CreateOperator(operatorSymbol), right));
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

    protected internal SqlBetweenExpression CreateBetweenExpression(ParseTreeNode betweenExprNode)
    {
        if (betweenExprNode.Term.Name != betweenExprTermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {betweenExprNode.Term.Name} which does not match {betweenExprTermName}", nameof(betweenExprNode));
        }

        // Children for BETWEEN:     expr BETWEEN expr AND expr => [0]=expr, [1]=BETWEEN, [2]=expr, [3]=AND, [4]=expr
        // Children for NOT BETWEEN: expr NOT BETWEEN expr AND expr => [0]=expr, [1]=NOT, [2]=BETWEEN, [3]=expr, [4]=AND, [5]=expr
        var isNegated = betweenExprNode.ChildNodes.Count == 6;

        if (isNegated)
        {
            var operand = Create(betweenExprNode.ChildNodes[0]);
            var lowerBound = Create(betweenExprNode.ChildNodes[3]);
            var upperBound = Create(betweenExprNode.ChildNodes[5]);
            return new(operand, lowerBound, upperBound, isNegated: true);
        }
        else
        {
            var operand = Create(betweenExprNode.ChildNodes[0]);
            var lowerBound = Create(betweenExprNode.ChildNodes[2]);
            var upperBound = Create(betweenExprNode.ChildNodes[4]);
            return new(operand, lowerBound, upperBound, isNegated: false);
        }
    }

    protected internal SqlBinaryExpression CreateIsNullExpression(ParseTreeNode isNullExpr)
    {
        if (isNullExpr.Term.Name != isNullExprTermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {isNullExpr.Term.Name} which does not match {isNullExprTermName}", nameof(isNullExpr));
        }

        // Children: expr IS NULL  => [0]=expr, [1]=IS, [2]=NULL
        //           expr IS NOT NULL => [0]=expr, [1]=IS, [2]=NOT, [3]=NULL
        var left = Create(isNullExpr.ChildNodes[0]);
        var isNotNull = isNullExpr.ChildNodes.Count == 4;
        var op = isNotNull ? SqlBinaryOperator.IsNotNull : SqlBinaryOperator.IsNull;

        return new(left, op, null);
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
            SqlBinaryOperator.IsNull => "IS NULL",
            SqlBinaryOperator.IsNotNull => "IS NOT NULL",
            _ => throw new ArgumentException($"Invalid binary operator {binaryOperator}", nameof(binaryOperator))
        };


    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
