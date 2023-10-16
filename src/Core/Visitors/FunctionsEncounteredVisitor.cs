using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Visitors;


/// <summary>
/// Determines whether any unspecified functions exist in the <see cref="SqlExpression"/> instances.  If <see cref="specifiedFunctionNames"/> is null or empty, then
/// this visitor can be used to determine if any functions exist in <see cref="SqlExpression"/> instances.
/// </summary>
public class FunctionsEncounteredVisitor : ISqlExpressionVisitor
{
    private readonly HashSet<string>? specifiedFunctionNames;

    public FunctionsEncounteredVisitor(HashSet<string>? specifiedFunctionNames = null)
    {
        this.specifiedFunctionNames = specifiedFunctionNames;
    }

    /// <summary>
    /// List of unspecified <see cref="SqlFunction" names that were encountered./>
    /// </summary>
    public HashSet<string> Unspecified { get; } = new();

    /// <summary>
    /// List of specified <see cref="SqlFunction" names that were encountered./>
    /// </summary>
    public HashSet<string> Specified { get; } = new();

    public void Visit(SqlBinaryExpression binExpr) { }

    public SqlExpression? Visit(SqlColumnRef column) => null;

    public SqlExpression? Visit(SqlParameter parameter) => null;

    public SqlExpression? Visit(SqlFunction function)
    {
        if (specifiedFunctionNames == null || !specifiedFunctionNames.Contains(function.FunctionName))
        {
            Unspecified.Add(function.FunctionName);
        }
        else
        {
            Specified.Add(function.FunctionName);
        }

        return null;
    }

    public SqlExpression? Visit(SqlLiteralValue value) => null;
}
