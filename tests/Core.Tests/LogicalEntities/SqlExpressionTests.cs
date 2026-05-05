using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.Visitors;
using Xunit;

namespace SqlBuildingBlocks.Core.Tests.LogicalEntities;

/// <summary>
/// Tests for the <see cref="SqlExpression"/> discriminated-union shape: every constructor must set
/// <see cref="SqlExpression.Kind"/> in agreement with the populated arm, the invariant must be
/// validated at construction, and the <c>AssumeExpressionLikeness</c> rewrite path must keep both
/// the populated arm and <see cref="SqlExpression.Kind"/> consistent on exit.
/// </summary>
public class SqlExpressionTests
{
    // --- Per-Kind construction tests: each constructor sets Kind and produces the right arm. ---

    [Fact]
    public void Ctor_FromColumn_SetsColumnKindAndExposesColumn()
    {
        var columnRef = new SqlColumnRef(null, "t", "c");

        var expr = new SqlExpression(columnRef);

        Assert.Equal(SqlExpressionKind.Column, expr.Kind);
        Assert.Same(columnRef, expr.Column);
        Assert.Equal("t.c", expr.ToExpressionString());
    }

    [Fact]
    public void Ctor_FromParameter_SetsParameterKindAndExposesParameter()
    {
        var parameter = new SqlParameter("p");

        var expr = new SqlExpression(parameter);

        Assert.Equal(SqlExpressionKind.Parameter, expr.Kind);
        Assert.Same(parameter, expr.Parameter);
        Assert.Equal("@p", expr.ToExpressionString());
    }

    [Fact]
    public void Ctor_FromFunction_SetsFunctionKindAndExposesFunction()
    {
        var function = new SqlFunction("UPPER");
        function.Arguments.Add(new SqlExpression(new SqlLiteralValue("a")));

        var expr = new SqlExpression(function);

        Assert.Equal(SqlExpressionKind.Function, expr.Kind);
        Assert.Same(function, expr.Function);
    }

    [Fact]
    public void Ctor_FromLiteralValue_SetsValueKindAndExposesValue()
    {
        var literal = new SqlLiteralValue(42);

        var expr = new SqlExpression(literal);

        Assert.Equal(SqlExpressionKind.Value, expr.Kind);
        Assert.Same(literal, expr.Value);
        Assert.Equal("42", expr.ToExpressionString());
    }

    [Fact]
    public void Ctor_FromBinaryExpression_SetsBinExprKindAndExposesBinExpr()
    {
        var bin = new SqlBinaryExpression(
            new SqlExpression(new SqlLiteralValue(1)),
            SqlBinaryOperator.Equal,
            new SqlExpression(new SqlLiteralValue(1)));

        var expr = new SqlExpression(bin);

        Assert.Equal(SqlExpressionKind.BinExpr, expr.Kind);
        Assert.Same(bin, expr.BinExpr);
    }

    [Fact]
    public void Ctor_FromBetweenExpression_SetsBetweenExprKindAndExposesBetweenExpr()
    {
        var between = new SqlBetweenExpression(
            new SqlExpression(new SqlLiteralValue(2)),
            new SqlExpression(new SqlLiteralValue(1)),
            new SqlExpression(new SqlLiteralValue(3)),
            isNegated: false);

        var expr = new SqlExpression(between);

        Assert.Equal(SqlExpressionKind.BetweenExpr, expr.Kind);
        Assert.Same(between, expr.BetweenExpr);
    }

    [Fact]
    public void Ctor_FromCaseExpression_SetsCaseExprKindAndExposesCaseExpr()
    {
        var when = new[]
        {
            (new SqlExpression(new SqlLiteralValue(true)), new SqlExpression(new SqlLiteralValue("yes")))
        };
        var caseExpr = new SqlCaseExpression(when, elseResult: new SqlExpression(new SqlLiteralValue("no")));

        var expr = new SqlExpression(caseExpr);

        Assert.Equal(SqlExpressionKind.CaseExpr, expr.Kind);
        Assert.Same(caseExpr, expr.CaseExpr);
    }

    [Fact]
    public void Ctor_FromExistsExpression_SetsExistsExprKindAndExposesExistsExpr()
    {
        var exists = new SqlExistsExpression(new SqlSelectDefinition(), isNegated: false);

        var expr = new SqlExpression(exists);

        Assert.Equal(SqlExpressionKind.ExistsExpr, expr.Kind);
        Assert.Same(exists, expr.ExistsExpr);
    }

    [Fact]
    public void Ctor_FromScalarSubquery_SetsScalarSubqueryKindAndExposesScalarSubqueryExpr()
    {
        var scalar = new SqlScalarSubqueryExpression(new SqlSelectDefinition());

        var expr = new SqlExpression(scalar);

        Assert.Equal(SqlExpressionKind.ScalarSubqueryExpr, expr.Kind);
        Assert.Same(scalar, expr.ScalarSubqueryExpr);
    }

    [Fact]
    public void Ctor_FromInList_SetsInListKindAndExposesInList()
    {
        var inList = new SqlInList(new[]
        {
            new SqlExpression(new SqlLiteralValue(1)),
            new SqlExpression(new SqlLiteralValue(2)),
        });

        var expr = new SqlExpression(inList);

        Assert.Equal(SqlExpressionKind.InList, expr.Kind);
        Assert.Same(inList, expr.InList);
    }

    [Fact]
    public void Ctor_FromCastExpression_SetsCastExprKindAndExposesCastExpr()
    {
        var cast = new SqlCastExpression(
            new SqlExpression(new SqlLiteralValue("123")),
            new SqlDataType("INTEGER"));

        var expr = new SqlExpression(cast);

        Assert.Equal(SqlExpressionKind.CastExpr, expr.Kind);
        Assert.Same(cast, expr.CastExpr);
    }

    [Fact]
    public void Ctor_FromArrayConstructor_SetsArrayConstructorKindAndExposesArrayConstructor()
    {
        var array = new SqlArrayConstructor(new[]
        {
            new SqlExpression(new SqlLiteralValue(1)),
        });

        var expr = new SqlExpression(array);

        Assert.Equal(SqlExpressionKind.ArrayConstructor, expr.Kind);
        Assert.Same(array, expr.ArrayConstructor);
    }

    [Fact]
    public void Ctor_FromArraySubscript_SetsArraySubscriptKindAndExposesArraySubscript()
    {
        var subscript = new SqlArraySubscript(
            new SqlExpression(new SqlColumnRef(null, "t", "c")),
            new SqlExpression(new SqlLiteralValue(1)));

        var expr = new SqlExpression(subscript);

        Assert.Equal(SqlExpressionKind.ArraySubscript, expr.Kind);
        Assert.Same(subscript, expr.ArraySubscript);
    }

    [Fact]
    public void Ctor_FromJsonExpression_SetsJsonExprKindAndExposesJsonExpr()
    {
        var json = new SqlJsonExpression(
            new SqlExpression(new SqlColumnRef(null, "t", "data")),
            SqlJsonOperator.Arrow,
            new SqlExpression(new SqlLiteralValue("key")));

        var expr = new SqlExpression(json);

        Assert.Equal(SqlExpressionKind.JsonExpr, expr.Kind);
        Assert.Same(json, expr.JsonExpr);
    }

    // --- GetExpression() routes by Kind for the literal-value arm consumers depend on. ---

    [Fact]
    public void GetExpression_OnValueArm_ReturnsConstantOrConverted()
    {
        var expr = new SqlExpression(new SqlLiteralValue(7));
        var companion = new SqlExpression(new SqlLiteralValue(0));
        var param = Expression.Parameter(typeof(DataRow), "dataRow");

        var linq = expr.GetExpression(new Dictionary<SqlTable, DataRow>(), new SqlTable(null, "t"), param, companion);

        Assert.NotNull(linq);
    }

    [Fact]
    public void GetExpression_OnParameterArm_ThrowsBecauseUnresolved()
    {
        var expr = new SqlExpression(new SqlParameter("@p"));
        var companion = new SqlExpression(new SqlLiteralValue(0));
        var param = Expression.Parameter(typeof(DataRow), "dataRow");

        Assert.Throws<Exception>(() =>
            expr.GetExpression(new Dictionary<SqlTable, DataRow>(), new SqlTable(null, "t"), param, companion));
    }

    [Fact]
    public void GetExpression_OnScalarSubqueryArm_ThrowsNotSupported()
    {
        var expr = new SqlExpression(new SqlScalarSubqueryExpression(new SqlSelectDefinition()));
        var companion = new SqlExpression(new SqlLiteralValue(0));
        var param = Expression.Parameter(typeof(DataRow), "dataRow");

        Assert.Throws<NotSupportedException>(() =>
            expr.GetExpression(new Dictionary<SqlTable, DataRow>(), new SqlTable(null, "t"), param, companion));
    }

    // --- Invariant tests. The 14 arm setters are private, so we exercise the invariant via reflection. ---

    [Fact]
    public void AssertInvariant_TwoArmsPopulated_Throws()
    {
        var expr = new SqlExpression(new SqlLiteralValue(1));

        // Force a second arm into the populated state via reflection. Setters are private, but the
        // private compiler-generated backing field is reachable. Then call AssertInvariant.
        SetArmDirectly(expr, nameof(SqlExpression.Parameter), new SqlParameter("@p"));

        var exception = InvokeAssertInvariant(expr);

        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("invariant violated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssertInvariant_ZeroArmsPopulated_Throws()
    {
        var expr = new SqlExpression(new SqlLiteralValue(1));

        // Clear the only populated arm so no arm matches Kind.
        SetArmDirectly(expr, nameof(SqlExpression.Value), null);

        var exception = InvokeAssertInvariant(expr);

        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("no arm is populated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssertInvariant_KindAndArmDisagree_Throws()
    {
        var expr = new SqlExpression(new SqlLiteralValue(1));

        // Swap the populated arm without updating Kind: clear Value, set Function. Kind still says Value.
        SetArmDirectly(expr, nameof(SqlExpression.Value), null);
        SetArmDirectly(expr, nameof(SqlExpression.Function), new SqlFunction("UPPER"));

        var exception = InvokeAssertInvariant(expr);

        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("populated arm is", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // --- Round-trip: visitor returns a different SqlExpression; AssumeExpressionLikeness mirrors it. ---

    [Fact]
    public void AssumeExpressionLikeness_ViaVisitor_RewritesArmAndKindTogether()
    {
        // The Parameter arm's Accept is the path that triggers HandleLeafNode → AssumeExpressionLikeness
        // when the visitor returns a non-null replacement. ResolveParametersVisitor swaps a SqlParameter
        // for a SqlLiteralValue-shaped SqlExpression, exercising the Kind-mirroring rewrite path.
        // Use the de-prefixed name "p" up front; the visitor's constructor strips a leading '@'
        // by mutating Name in place, which would change the dictionary hash and cause a lookup
        // miss. Constructing with "p" avoids that pre-existing wrinkle.
        var pNamed = new SqlParameter("p");
        var expr = new SqlExpression(pNamed);
        Assert.Equal(SqlExpressionKind.Parameter, expr.Kind);

        var bindings = new Dictionary<SqlParameter, SqlLiteralValue>
        {
            [pNamed] = new SqlLiteralValue(99),
        };
        var visitor = new ResolveParametersVisitor(bindings);

        expr.Accept(visitor);

        Assert.Equal(SqlExpressionKind.Value, expr.Kind);
        Assert.NotNull(expr.Value);
        Assert.Null(expr.Parameter);
    }

    // --- Test helpers (reflection-based; setters are private). ---

    private static void SetArmDirectly(SqlExpression expr, string propertyName, object value)
    {
        var prop = typeof(SqlExpression).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(prop);
        // Use the property's private setter via the non-public accessor.
        var setter = prop.GetSetMethod(nonPublic: true);
        Assert.NotNull(setter);
        setter.Invoke(expr, new[] { value });
    }

    private static Exception InvokeAssertInvariant(SqlExpression expr)
    {
        var method = typeof(SqlExpression).GetMethod("AssertInvariant", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        try
        {
            method.Invoke(expr, null);
            return null;
        }
        catch (TargetInvocationException tie)
        {
            return tie.InnerException;
        }
    }
}
