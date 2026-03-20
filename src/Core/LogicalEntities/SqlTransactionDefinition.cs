namespace SqlBuildingBlocks.LogicalEntities;

public class SqlTransactionDefinition
{
    /// <summary>
    /// The kind of transaction statement (Begin, Commit, Rollback).
    /// </summary>
    public SqlTransactionKind Kind { get; set; }

    /// <summary>
    /// Optional isolation level for BEGIN TRANSACTION (e.g., "READ COMMITTED", "SERIALIZABLE").
    /// </summary>
    public string? IsolationLevel { get; set; }
}
