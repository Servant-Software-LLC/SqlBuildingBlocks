using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.PostgreSQL;

/// <summary>
/// PostgreSQL-specific data type parser that extends the core <see cref="SqlBuildingBlocks.DataType"/>
/// with PostgreSQL-specific types such as TEXT, JSONB, JSON, UUID, BYTEA, etc.
/// Also supports array type syntax (e.g. integer[], text[][]).
/// </summary>
public class DataType : SqlBuildingBlocks.DataType
{
    private const string arrayBracketsTermName = "arrayBrackets";
    private const string arrayBracketsOptTermName = "arrayBracketsOpt";

    public DataType(Grammar grammar) : base(grammar)
    {
        // Only add types NOT already in base DataType.
        // Base already has: BOOL, BOOLEAN, DATE, TIME, TIMESTAMP, DECIMAL, NUMERIC, REAL, FLOAT,
        //                   SMALLINT, INTEGER, INT, BIGINT, CHARACTER, CHAR, VARCHAR, NCHAR, NVARCHAR,
        //                   SERIAL, BIGSERIAL, SMALLSERIAL
        dataTypeNames.Rule |= grammar.ToTerm("TEXT") | "JSON" | "JSONB" | "UUID" | "BYTEA"
                             | "INTERVAL" | "MONEY" | "INET" | "CIDR" | "MACADDR";

        // Array type suffix: [] or [][] etc.
        var LBRACKET = grammar.ToTerm("[");
        var RBRACKET = grammar.ToTerm("]");

        var arrayBracketPair = new NonTerminal("arrayBracketPair");
        arrayBracketPair.Rule = LBRACKET + RBRACKET;

        var arrayBrackets = new NonTerminal(arrayBracketsTermName);
        grammar.MakePlusRule(arrayBrackets, arrayBracketPair);

        var arrayBracketsOpt = new NonTerminal(arrayBracketsOptTermName);
        arrayBracketsOpt.Rule = grammar.Empty | arrayBrackets;

        // Mark transient so that when brackets are present, the arrayBrackets node
        // appears directly as a child; when empty, no child is added.
        grammar.MarkTransient(arrayBracketsOpt);

        // Extend the rule: dataTypeNames + typeParamsOpt + arrayBracketsOpt
        Rule = Rule + arrayBracketsOpt;
    }

    public override SqlDataType Create(ParseTreeNode dataType)
    {
        if (dataType.Term.Name != TermName)
            throw new ArgumentException($"Cannot create building block of type {typeof(SqlDataType)}.  The TermName for node is {dataType.Term.Name} which does not match {TermName}", nameof(dataType));

        var dataTypeName = dataType.ChildNodes[0].Term.Name;
        SqlDataType sqlDataType = new(dataTypeName);

        var typeParams = dataType.ChildNodes[1];
        if (!SetTypeParams(typeParams, sqlDataType))
            throw new ArgumentException($"In CREATE TABLE the column for data type {dataTypeName} had {typeParams.ChildNodes.Count} parameters, which is not allowed.");

        // Parse optional array brackets (child index 2)
        if (dataType.ChildNodes.Count > 2)
        {
            var arrayBracketsNode = dataType.ChildNodes[2];
            if (arrayBracketsNode.Term.Name == arrayBracketsTermName)
            {
                sqlDataType.ArrayDimensions = arrayBracketsNode.ChildNodes.Count;
            }
        }

        return sqlDataType;
    }

    protected override bool SetTypeParams(ParseTreeNode typeParams, SqlDataType sqlDataType)
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
