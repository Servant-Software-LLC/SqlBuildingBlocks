using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.QueryProcessing;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlFunction
{
    public SqlFunction(string functionName)
    {
        FunctionName = functionName;
    }

    public string FunctionName { get; }
    public List<SqlExpression> Arguments { get; } = new List<SqlExpression>();

    public Type ValueType { get; set; } = typeof(string);

    /// <summary>
    /// If this instance, isn't replaced in the <see cref="SqlExpression" instances of a SQL statement before the
    /// <see cref="QueryEngine.Query"/> is called, then this property must be set in order for the <see cref="QueryEngine"/>
    /// to be able to calculate its value.  This situation normally arises when the context of this function is 
    /// dependent on the columns specified in the <see cref="Arguments"/> property of this class.  For example, />
    /// <see href="https://learn.microsoft.com/en-us/sql/t-sql/functions/upper-transact-sql?view=sql-server-ver16">UPPER</see> </summary>
    public Func<object>? CalculateValue { get; set; }

    public SqlExpression? Accept(ISqlExpressionVisitor visitor) => visitor.Visit(this);

    public string ToExpressionString() => ToString();

    public override string ToString()
    {
        string arguments = string.Join(", ", Arguments.Select(arg => arg.ToString()));
        return $"{FunctionName}({arguments})";
    }
}
