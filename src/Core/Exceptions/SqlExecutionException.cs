using System;
using System.Runtime.Serialization;

namespace SqlBuildingBlocks.Exceptions;

/// <summary>
/// Thrown when the query engine encounters a runtime data-shape error while executing
/// a parsed SQL statement. This is distinct from <see cref="NotImplementedException"/>
/// (planned-but-not-yet-built features) and <see cref="NotSupportedException"/>
/// (features intentionally out of scope).
///
/// Examples of <see cref="SqlExecutionException"/> conditions:
/// - A SELECT references a function whose value cannot be resolved at execution time.
/// - The result set has duplicate column names with no table reference for disambiguation.
/// - An internal invariant is violated by an empty join list passed to a recursive resolver.
/// </summary>
[Serializable]
public class SqlExecutionException : Exception
{
    public SqlExecutionException()
    {
    }

    public SqlExecutionException(string message)
        : base(message)
    {
    }

    public SqlExecutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    protected SqlExecutionException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
