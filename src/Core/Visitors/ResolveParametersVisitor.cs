using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using System.Data.Common;

namespace SqlBuildingBlocks.Visitors;

public class ResolveParametersVisitor : ISqlExpressionVisitor, ISqlValueVisitor
{
    private readonly Dictionary<SqlParameter, SqlLiteralValue>? namedParameters;
    private readonly IList<SqlLiteralValue>? positionalParameterValues;
    private int positionalParameterIndex = 0;

    /// <summary>
    /// Replaces named <see cref="SqlParameter"/> in a <see cref="SqlExpression"/> with their <see cref="SqlLiteralValue"/> as caller provided in the dictionary
    /// </summary>
    /// <param name="namedParameters"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public ResolveParametersVisitor(Dictionary<SqlParameter, SqlLiteralValue> namedParameters)
    {
        if (namedParameters == null)
            throw new ArgumentNullException(nameof(namedParameters));

        if (namedParameters.Any(pair => pair.Key.Type != SqlParameter.ParameterType.Named))
            throw new ArgumentException($"All the keys in the {namedParameters.GetType().Name} must have a {nameof(SqlParameter.Type)} == {nameof(SqlParameter.ParameterType.Named)}", nameof(namedParameters));

        //Be forgiving if the SqlParameter.Name includes the '@' prefix
        foreach (KeyValuePair<SqlParameter, SqlLiteralValue> pair in namedParameters)
        {
            pair.Key.Name = ParameterNameOnly(pair.Key.Name!);
        }

        this.namedParameters = namedParameters;
    }

    /// <summary>
    /// Convenience ctor to provide named parameters.
    /// </summary>
    /// <param name="parameters"></param>
    public ResolveParametersVisitor(DbParameterCollection parameters)
    {
        Dictionary<SqlParameter, SqlLiteralValue> namedParameters = new();
        foreach (DbParameter parameter in parameters)
        {
            SqlParameter sqlParameter = new(ParameterNameOnly(parameter.ParameterName));
            SqlLiteralValue sqlLiteralValue = new(parameter.Value);

            namedParameters.Add(sqlParameter, sqlLiteralValue);
        }

        this.namedParameters = namedParameters;
    }

    /// <summary>
    /// Replaces positional parameters in a <see cref="SqlExpression"/> with their <see cref="SqlLiteralValue"/> as caller provided in the list.
    /// </summary>
    /// <param name="positionalParameterValues"></param>
    public ResolveParametersVisitor(IList<SqlLiteralValue>? positionalParameterValues) =>
        this.positionalParameterValues = positionalParameterValues ?? throw new ArgumentNullException(nameof(positionalParameterValues));

    /// <summary>
    /// Allows reuse of the <see cref="ParameterToValueConverter"/>
    /// </summary>
    /// <param name="parameterToValueConverter"></param>
    public ResolveParametersVisitor(ResolveParametersVisitor resolveParametersVisitor)
    {
        namedParameters = resolveParametersVisitor.namedParameters;
        positionalParameterValues = resolveParametersVisitor.positionalParameterValues;
    }
        
    public void Visit(SqlBinaryExpression binExpr) { }

    public SqlExpression? Visit(SqlColumnRef column) => null;

    public SqlExpression? Visit(SqlParameter parameter)
    {
        var literalValue = ((ISqlValueVisitor)this).Visit(parameter);
        if (literalValue == null)
            return null;

        return new(literalValue);
    }

    public SqlExpression? Visit(SqlFunction function) => null;

    public SqlExpression? Visit(SqlLiteralValue value) => null;

    public SqlLimitValue? Visit(SqlLimitValue limit)
    {
        if (limit.Parameter != null)
            return limit.Parameter.Accept((ISqlValueVisitor)this);

        return limit;
    }

    SqlLiteralValue? ISqlValueVisitor.Visit(SqlParameter parameter)
    {
        if (parameter.Type == SqlParameter.ParameterType.Named)
        {
            if (namedParameters != null && namedParameters.TryGetValue(parameter, out var value))
            {
                return value;
            }

            throw new ArgumentException($"No parameter by the name of {parameter.Name} in {nameof(namedParameters)}", nameof(namedParameters));
        }
        else
        {
            if (positionalParameterValues == null)
                throw new ArgumentNullException($"Named parameters were provided to the ctor of {nameof(ResolveParametersVisitor)}, but the parameter {parameter} was a positional parameter");

            if (positionalParameterIndex >= positionalParameterValues.Count)
                throw new ArgumentException($"More unnamed parameters visited then values that were provided in IList<SqlLiteralValue>", nameof(parameter));

            return positionalParameterValues[positionalParameterIndex++];
        }
    }

    SqlLiteralValue? ISqlValueVisitor.Visit(SqlFunction function) => null;

    SqlLiteralValue? ISqlValueVisitor.Visit(SqlLiteralValue value) => null;

    private string ParameterNameOnly(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName))
            return string.Empty;

        return parameterName[0] == '@' ? parameterName.Substring(1) : parameterName;
    }

}
