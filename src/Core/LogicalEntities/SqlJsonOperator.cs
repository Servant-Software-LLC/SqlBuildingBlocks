namespace SqlBuildingBlocks.LogicalEntities;

/// <summary>
/// PostgreSQL JSON/JSONB operators.
/// </summary>
public enum SqlJsonOperator
{
    /// <summary><c>-&gt;</c> — Get JSON object field by key (returns JSON).</summary>
    Arrow,

    /// <summary><c>-&gt;&gt;</c> — Get JSON object field by key (returns text).</summary>
    DoubleArrow,

    /// <summary><c>#&gt;</c> — Get JSON value at path (returns JSON).</summary>
    HashArrow,

    /// <summary><c>#&gt;&gt;</c> — Get JSON value at path (returns text).</summary>
    HashDoubleArrow,

    /// <summary><c>@&gt;</c> — JSON contains (left contains right).</summary>
    Contains,

    /// <summary><c>&lt;@</c> — JSON contained by (left is contained by right).</summary>
    ContainedBy,

    /// <summary><c>?</c> — Does the key exist in the JSON object?</summary>
    KeyExists,

    /// <summary><c>?|</c> — Do any of the keys exist?</summary>
    AnyKeyExists,

    /// <summary><c>?&amp;</c> — Do all of the keys exist?</summary>
    AllKeysExist
}
