using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.MySQL;

/// <summary>
/// MySQL-specific expression parser that extends the core <see cref="SqlBuildingBlocks.Expr"/>
/// with support for INTERVAL expressions used in DATE_ADD/DATE_SUB.
/// e.g. DATE_ADD(date, INTERVAL 1 DAY)
/// </summary>
public class Expr : SqlBuildingBlocks.Expr
{
    private const string intervalExprTermName = "intervalExpr";

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.
    /// Don't forget to call <see cref="SqlBuildingBlocks.Expr.InitializeRule"/> followed by <see cref="AddIntervalSupport"/>.
    /// </summary>
    public Expr(Grammar grammar) : base(grammar) { }
    public Expr(Grammar grammar, Id id) : base(grammar, id) { }
    public Expr(Grammar grammar, Id id, LiteralValue literalValue, Parameter parameter)
        : base(grammar, id, literalValue, parameter) { }

    /// <summary>
    /// Adds INTERVAL expression support to the grammar rule.
    /// Must be called AFTER <see cref="SqlBuildingBlocks.Expr.InitializeRule"/>.
    /// </summary>
    public void AddIntervalSupport(Grammar grammar)
    {
        var INTERVAL = grammar.ToTerm("INTERVAL");

        var intervalUnit = new NonTerminal("intervalUnit");
        intervalUnit.Rule = grammar.ToTerm("MICROSECOND") | "SECOND" | "MINUTE" | "HOUR"
                          | "DAY" | "WEEK" | "MONTH" | "QUARTER" | "YEAR"
                          | "SECOND_MICROSECOND" | "MINUTE_MICROSECOND" | "MINUTE_SECOND"
                          | "HOUR_MICROSECOND" | "HOUR_SECOND" | "HOUR_MINUTE"
                          | "DAY_MICROSECOND" | "DAY_SECOND" | "DAY_MINUTE" | "DAY_HOUR"
                          | "YEAR_MONTH";

        var intervalExpr = new NonTerminal(intervalExprTermName);
        intervalExpr.Rule = INTERVAL + this + intervalUnit;

        grammar.MarkPunctuation(INTERVAL);

        // Extend the Expr rule to include interval expressions
        Rule |= intervalExpr;
    }

    public override SqlExpression Create(ParseTreeNode expression)
    {
        if (expression.Term.Name == intervalExprTermName)
        {
            return CreateIntervalExpression(expression);
        }

        return base.Create(expression);
    }

    private SqlExpression CreateIntervalExpression(ParseTreeNode intervalExprNode)
    {
        // After INTERVAL is stripped as punctuation, children are: [valueExpr, unitToken]
        var valueExpr = Create(intervalExprNode.ChildNodes[0]);
        var unitName = intervalExprNode.ChildNodes[1].ChildNodes[0].Token.Text.ToUpperInvariant();

        // Represent as SqlFunction("INTERVAL") with the value expression and unit as arguments
        var intervalFunc = new SqlFunction("INTERVAL");
        intervalFunc.Arguments.Add(valueExpr);
        intervalFunc.Arguments.Add(new SqlExpression(new SqlLiteralValue(unitName)));

        return new SqlExpression(intervalFunc);
    }
}
