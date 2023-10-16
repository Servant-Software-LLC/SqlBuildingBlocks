using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.SQLServer;

public class DataType : SqlBuildingBlocks.DataType
{
    public DataType(Grammar grammar)
        : base(grammar)
    {
        dataTypeNames.Rule |= grammar.ToTerm("DATETIME") | "DOUBLE" | "IMAGE" | "TEXT" | "NTEXT" | "BIT";
    }

    public new static Type? ToSystemType(string dataTypeName) =>
        dataTypeName switch
        {
            "DATETIME" => typeof(DateTime),
            "DOUBLE" => typeof(double),
            "IMAGE" => typeof(byte[]),
            "TEXT" => typeof(string),
            "NTEXT" => typeof(string),
            "BIT" => typeof(bool),
            _ => SqlBuildingBlocks.DataType.ToSystemType(dataTypeName)
        };

    protected override bool SetTypeParams(ParseTreeNode typeParams, SqlDataType sqlDataType)
    {
        if (base.SetTypeParams(typeParams, sqlDataType))
            return true;

        switch (typeParams.ChildNodes.Count)
        {
            case 0:
                if (sqlDataType.Name == "DATETIME" || sqlDataType.Name == "DOUBLE" || sqlDataType.Name == "IMAGE" || sqlDataType.Name == "TEXT" ||
                    sqlDataType.Name == "NTEXT" || sqlDataType.Name == "BIT") 
                    return true;
                break;
            case 1:
                if (sqlDataType.Name == "BIT")
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
