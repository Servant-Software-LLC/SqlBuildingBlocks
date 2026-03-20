using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.PostgreSQL;

/// <summary>
/// PostgreSQL-specific expression parser that extends the core <see cref="SqlBuildingBlocks.Expr"/>
/// with support for:
/// - The :: cast operator (e.g. value::integer, col::text, '2023-01-01'::date)
/// - ARRAY constructor syntax (e.g. ARRAY[1, 2, 3])
/// - Array subscript syntax (e.g. col[1], col[1:3])
/// - ANY(ARRAY[...]) and ALL(ARRAY[...])
/// - JSON operators (->, ->>, #>, #>>, @>, <@, ?, ?|, ?&)
/// </summary>
public class Expr : SqlBuildingBlocks.Expr
{
    private const string pgCastExprTermName = "pgCastExpr";
    private const string pgArrayConstructorTermName = "pgArrayConstructor";
    private const string pgArraySubscriptTermName = "pgArraySubscript";
    private const string pgJsonExprTermName = "pgJsonExpr";

    private SqlBuildingBlocks.DataType? pgDataType;

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.
    /// Don't forget to call <see cref="SqlBuildingBlocks.Expr.InitializeRule"/> followed by <see cref="AddPgCastSupport"/>.
    /// </summary>
    public Expr(Grammar grammar) : base(grammar) { }
    public Expr(Grammar grammar, Id id) : base(grammar, id) { }
    public Expr(Grammar grammar, Id id, LiteralValue literalValue, Parameter parameter)
        : base(grammar, id, literalValue, parameter) { }

    /// <summary>
    /// Adds PostgreSQL :: cast operator support to the grammar rule.
    /// Must be called AFTER <see cref="SqlBuildingBlocks.Expr.InitializeRule"/>.
    /// </summary>
    public void AddPgCastSupport(Grammar grammar, SqlBuildingBlocks.DataType dataType)
    {
        pgDataType = dataType;

        var DOUBLE_COLON = grammar.ToTerm("::");

        var pgCastExpr = new NonTerminal(pgCastExprTermName);
        pgCastExpr.Rule = this + DOUBLE_COLON + dataType;

        // Register :: with high precedence so it binds tightly (higher than arithmetic)
        grammar.RegisterOperators(11, Associativity.Left, DOUBLE_COLON);

        // Mark :: as punctuation so it's stripped from the parse tree
        grammar.MarkPunctuation(DOUBLE_COLON);

        // Extend the Expr rule to include PostgreSQL cast expressions
        Rule |= pgCastExpr;
    }

    /// <summary>
    /// Adds PostgreSQL ARRAY constructor support (e.g. ARRAY[1, 2, 3]).
    /// Must be called AFTER <see cref="SqlBuildingBlocks.Expr.InitializeRule"/>.
    /// </summary>
    public void AddArrayConstructorSupport(Grammar grammar)
    {
        var ARRAY = grammar.ToTerm("ARRAY");
        var LBRACKET = grammar.ToTerm("[");
        var RBRACKET = grammar.ToTerm("]");

        var arrayExprList = new ExprList(grammar, this);

        var pgArrayConstructor = new NonTerminal(pgArrayConstructorTermName);
        pgArrayConstructor.Rule = ARRAY + LBRACKET + arrayExprList + RBRACKET;

        grammar.MarkPunctuation(ARRAY, LBRACKET, RBRACKET);
        grammar.MarkReservedWords("ARRAY");

        Rule |= pgArrayConstructor;
    }

    /// <summary>
    /// Adds PostgreSQL array subscript support (e.g. col[1], col[1:3]).
    /// Must be called AFTER <see cref="SqlBuildingBlocks.Expr.InitializeRule"/>.
    /// </summary>
    public void AddArraySubscriptSupport(Grammar grammar)
    {
        var LBRACKET = grammar.ToTerm("[");
        var RBRACKET = grammar.ToTerm("]");
        var COLON = grammar.ToTerm(":");

        var pgArraySubscript = new NonTerminal(pgArraySubscriptTermName);
        // Single index: expr[expr]
        // Slice: expr[expr:expr]
        pgArraySubscript.Rule = this + LBRACKET + this + RBRACKET
                              | this + LBRACKET + this + COLON + this + RBRACKET;

        grammar.RegisterOperators(12, Associativity.Left, LBRACKET);
        grammar.MarkPunctuation(LBRACKET, RBRACKET, COLON);

        Rule |= pgArraySubscript;
    }

    /// <summary>
    /// Adds PostgreSQL JSON operator support (->, ->>, #>, #>>, @>, &lt;@, ?, ?|, ?&amp;).
    /// Must be called AFTER <see cref="SqlBuildingBlocks.Expr.InitializeRule"/>.
    /// </summary>
    public void AddJsonOperatorSupport(Grammar grammar)
    {
        // Order matters: longer operators must be registered first so the scanner
        // doesn't greedily match a shorter prefix (e.g. -> before ->>).
        var DOUBLE_ARROW = grammar.ToTerm("->>");
        var ARROW = grammar.ToTerm("->");
        var HASH_DOUBLE_ARROW = grammar.ToTerm("#>>");
        var HASH_ARROW = grammar.ToTerm("#>");
        var CONTAINS = grammar.ToTerm("@>");
        var CONTAINED_BY = grammar.ToTerm("<@");
        var KEY_EXISTS = grammar.ToTerm("?");
        var ANY_KEY_EXISTS = grammar.ToTerm("?|");
        var ALL_KEYS_EXIST = grammar.ToTerm("?&");

        var pgJsonOp = new NonTerminal("pgJsonOp");
        pgJsonOp.Rule = ARROW | DOUBLE_ARROW | HASH_ARROW | HASH_DOUBLE_ARROW
                      | CONTAINS | CONTAINED_BY
                      | KEY_EXISTS | ANY_KEY_EXISTS | ALL_KEYS_EXIST;

        var pgJsonExpr = new NonTerminal(pgJsonExprTermName);
        pgJsonExpr.Rule = this + pgJsonOp + this;

        // Register at precedence 8 (same level as comparison operators)
        // so JSON access binds tighter than AND/OR but at the same level as = / < / >.
        grammar.RegisterOperators(8, Associativity.Left,
            ARROW, DOUBLE_ARROW, HASH_ARROW, HASH_DOUBLE_ARROW,
            CONTAINS, CONTAINED_BY,
            KEY_EXISTS, ANY_KEY_EXISTS, ALL_KEYS_EXIST);

        pgJsonOp.SetFlag(TermFlags.InheritPrecedence);

        Rule |= pgJsonExpr;
    }

    public override SqlExpression Create(ParseTreeNode expression)
    {
        if (expression.Term.Name == pgCastExprTermName)
        {
            return CreatePgCastExpression(expression);
        }

        if (expression.Term.Name == pgArrayConstructorTermName)
        {
            return CreateArrayConstructorExpression(expression);
        }

        if (expression.Term.Name == pgArraySubscriptTermName)
        {
            return CreateArraySubscriptExpression(expression);
        }

        if (expression.Term.Name == pgJsonExprTermName)
        {
            return CreateJsonExpression(expression);
        }

        return base.Create(expression);
    }

    private SqlExpression CreatePgCastExpression(ParseTreeNode pgCastExprNode)
    {
        // :: is marked as punctuation so children are: [expr, dataType]
        var exprNode = pgCastExprNode.ChildNodes[0];
        var dataTypeNode = pgCastExprNode.ChildNodes[1];

        var innerExpression = Create(exprNode);
        var sqlDataType = pgDataType!.Create(dataTypeNode);

        return new SqlExpression(new SqlCastExpression(innerExpression, sqlDataType));
    }

    private SqlExpression CreateArrayConstructorExpression(ParseTreeNode arrayNode)
    {
        // ARRAY, [, ] are punctuation, so children are: [exprList]
        var exprListNode = arrayNode.ChildNodes[0];
        var items = exprListNode.ChildNodes.Select(Create).ToList();
        return new SqlExpression(new SqlArrayConstructor(items));
    }

    private SqlExpression CreateJsonExpression(ParseTreeNode jsonExprNode)
    {
        // Children: [left, pgJsonOp, right]
        // pgJsonOp has one child token identifying the operator.
        var leftNode = jsonExprNode.ChildNodes[0];
        var opNode = jsonExprNode.ChildNodes[1];
        var rightNode = jsonExprNode.ChildNodes[2];

        var left = Create(leftNode);
        var right = Create(rightNode);

        var opText = opNode.ChildNodes[0].Token.Text;
        var jsonOp = opText switch
        {
            "->" => SqlJsonOperator.Arrow,
            "->>" => SqlJsonOperator.DoubleArrow,
            "#>" => SqlJsonOperator.HashArrow,
            "#>>" => SqlJsonOperator.HashDoubleArrow,
            "@>" => SqlJsonOperator.Contains,
            "<@" => SqlJsonOperator.ContainedBy,
            "?" => SqlJsonOperator.KeyExists,
            "?|" => SqlJsonOperator.AnyKeyExists,
            "?&" => SqlJsonOperator.AllKeysExist,
            _ => throw new ArgumentException($"Unknown PostgreSQL JSON operator: {opText}")
        };

        return new SqlExpression(new SqlJsonExpression(left, jsonOp, right));
    }

    private SqlExpression CreateArraySubscriptExpression(ParseTreeNode subscriptNode)
    {
        // [, ], : are punctuation
        if (subscriptNode.ChildNodes.Count == 2)
        {
            // Single index: [expr, index]
            var arrayExpr = Create(subscriptNode.ChildNodes[0]);
            var indexExpr = Create(subscriptNode.ChildNodes[1]);
            return new SqlExpression(new SqlArraySubscript(arrayExpr, indexExpr));
        }
        else
        {
            // Slice: [expr, lower, upper]
            var arrayExpr = Create(subscriptNode.ChildNodes[0]);
            var lowerBound = Create(subscriptNode.ChildNodes[1]);
            var upperBound = Create(subscriptNode.ChildNodes[2]);
            return new SqlExpression(new SqlArraySubscript(arrayExpr, lowerBound, upperBound));
        }
    }
}
