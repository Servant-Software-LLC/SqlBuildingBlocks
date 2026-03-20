using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.PostgreSQL;

/// <summary>
/// PostgreSQL-specific expression parser that extends the core <see cref="SqlBuildingBlocks.Expr"/>
/// with support for the :: cast operator (e.g. value::integer, col::text, '2023-01-01'::date).
/// Chained casts are supported naturally (e.g. expr::type1::type2).
/// </summary>
public class Expr : SqlBuildingBlocks.Expr
{
    private const string pgCastExprTermName = "pgCastExpr";

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

    public override SqlExpression Create(ParseTreeNode expression)
    {
        if (expression.Term.Name == pgCastExprTermName)
        {
            return CreatePgCastExpression(expression);
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
}
