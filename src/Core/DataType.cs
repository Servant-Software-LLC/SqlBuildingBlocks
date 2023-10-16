using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class DataType : NonTerminal
{
    protected NonTerminal typeParamsOpt;
    protected NonTerminal dataTypeNames;

    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public DataType(Grammar grammar)
        : base(TermName)
    {
        var COMMA = grammar.ToTerm(",");
        var number = new NumberLiteral("number");

        typeParamsOpt = new NonTerminal("typeParams");
        typeParamsOpt.Rule = "(" + number + ")" | "(" + number + COMMA + number + ")" | grammar.Empty;

        dataTypeNames = new NonTerminal("dataTypeNames");
        dataTypeNames.Rule = grammar.ToTerm("BOOL") | "BOOLEAN" | "DATE" | "TIME" | "TIMESTAMP" | "DECIMAL" | "NUMERIC" | "REAL" | "FLOAT" | "SMALLINT" | "INTEGER" | "INT"
                                     | "CHARACTER" | "CHAR" | "VARCHAR" | "NCHAR" | "NVARCHAR";

        Rule = dataTypeNames + typeParamsOpt;

        grammar.MarkTransient(dataTypeNames);
    }

    public static Type? ToSystemType(string dataTypeName) =>
        dataTypeName switch
        {
            "BOOL" => typeof(bool),
            "BOOLEAN" => typeof(bool),
            "DATE" => typeof(DateTime),
            "TIME" => typeof(TimeSpan),
            "TIMESTAMP" => typeof(long),
            "DECIMAL" => typeof(decimal),
            "NUMERIC" => typeof(decimal),
            "REAL" => typeof(float),
            "FLOAT" => typeof(double),
            "SMALLINT" => typeof(short),
            "INTEGER" => typeof(int),
            "INT" => typeof(int),
            "CHARACTER" => typeof(string),
            "CHAR" => typeof(string),
            "VARCHAR" => typeof(string),
            "NCHAR" => typeof(string),
            "NVARCHAR" => typeof(string),
            _ => null
        };

    public virtual SqlDataType Create(ParseTreeNode dataType)
    {
        if (dataType.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {dataType.Term.Name} which does not match {TermName}", nameof(dataType));
        }

        //NOTE: The Token.ValueString for the grammar in the FileBased.DataProviders repo was having Token.ValueString
        //      of this ChildNode return a string in all lowercase.  Could not reproduce in a unit test though, but fixed
        //      the issue by using Term.Name instead.
        var dataTypeName = dataType.ChildNodes[0].Term.Name;
        SqlDataType sqlDataType = new(dataTypeName);

        var typeParams = dataType.ChildNodes[1];
        if (!SetTypeParams(typeParams, sqlDataType))
            throw new ArgumentException($"In CREATE TABLE the column for data type {dataTypeName} had {typeParams.ChildNodes.Count} parameters, which is not allowed.");

        return sqlDataType;
    }

    protected virtual bool SetTypeParams(ParseTreeNode typeParams, SqlDataType sqlDataType)
    {
        switch(typeParams.ChildNodes.Count)
        {
            case 0:
                if (sqlDataType.Name == "BOOL" || sqlDataType.Name == "BOOLEAN" ||sqlDataType.Name == "DATE" || sqlDataType.Name == "TIME" || 
                    sqlDataType.Name == "TIMESTAMP" || sqlDataType.Name == "REAL" || sqlDataType.Name == "FLOAT" || sqlDataType.Name == "SMALLINT" || 
                    sqlDataType.Name == "INTEGER" || sqlDataType.Name == "INT" || sqlDataType.Name == "CHARACTER" || sqlDataType.Name == "CHAR" || 
                    sqlDataType.Name == "VARCHAR" || sqlDataType.Name == "NCHAR" || sqlDataType.Name == "NVARCHAR")
                    return true;
                break;
            case 1:
                if (sqlDataType.Name == "CHARACTER" || sqlDataType.Name == "CHAR" || sqlDataType.Name == "VARCHAR" ||
                    sqlDataType.Name == "NCHAR" || sqlDataType.Name == "NVARCHAR")
                {
                    sqlDataType.Length = (int)typeParams.ChildNodes[0].Token.Value;
                    return true;
                }
                if (sqlDataType.Name == "TIME" || sqlDataType.Name == "TIMESTAMP")
                {
                    sqlDataType.Precision = (int)typeParams.ChildNodes[0].Token.Value;
                    return true;
                }
                break;
            case 2:
                if (sqlDataType.Name == "DECIMAL" || sqlDataType.Name == "NUMERIC")
                {
                    sqlDataType.Precision = (int)typeParams.ChildNodes[0].Token.Value;
                    sqlDataType.Scale = (int)typeParams.ChildNodes[1].Token.Value;
                    return true;
                }
                break;
        }

        return false;
    }
}
