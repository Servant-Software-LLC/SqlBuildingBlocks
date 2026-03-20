using Irony.Parsing;

namespace SqlBuildingBlocks.Grammars.PostgreSQL;

/// <summary>
/// PostgreSQL-specific data type parser that extends the core <see cref="SqlBuildingBlocks.DataType"/>
/// with PostgreSQL-specific types such as TEXT, JSONB, JSON, UUID, BYTEA, etc.
/// </summary>
public class DataType : SqlBuildingBlocks.DataType
{
    public DataType(Grammar grammar) : base(grammar)
    {
        // Only add types NOT already in base DataType.
        // Base already has: BOOL, BOOLEAN, DATE, TIME, TIMESTAMP, DECIMAL, NUMERIC, REAL, FLOAT,
        //                   SMALLINT, INTEGER, INT, BIGINT, CHARACTER, CHAR, VARCHAR, NCHAR, NVARCHAR,
        //                   SERIAL, BIGSERIAL, SMALLSERIAL
        dataTypeNames.Rule |= grammar.ToTerm("TEXT") | "JSON" | "JSONB" | "UUID" | "BYTEA"
                             | "INTERVAL" | "MONEY" | "INET" | "CIDR" | "MACADDR";
    }

    protected override bool SetTypeParams(ParseTreeNode typeParams, LogicalEntities.SqlDataType sqlDataType)
    {
        if (base.SetTypeParams(typeParams, sqlDataType))
            return true;

        // Filter to only number-bearing children
        var numericChildren = typeParams.ChildNodes
            .Where(n => n.Token != null && n.Token.Terminal is NumberLiteral)
            .ToList();

        // PostgreSQL-specific types that take no parameters
        if (numericChildren.Count == 0)
        {
            switch (sqlDataType.Name)
            {
                case "TEXT":
                case "JSON":
                case "JSONB":
                case "UUID":
                case "BYTEA":
                case "INTERVAL":
                case "MONEY":
                case "INET":
                case "CIDR":
                case "MACADDR":
                    return true;
            }
        }

        return false;
    }
}
