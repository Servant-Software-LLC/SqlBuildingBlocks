namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// Specifies the action to take when a conflict occurs during INSERT.
/// </summary>
public enum SqlUpsertAction
{
    /// <summary>PostgreSQL: ON CONFLICT ... DO NOTHING</summary>
    DoNothing,

    /// <summary>PostgreSQL: ON CONFLICT ... DO UPDATE SET ... / MySQL: ON DUPLICATE KEY UPDATE ...</summary>
    Update,
}
