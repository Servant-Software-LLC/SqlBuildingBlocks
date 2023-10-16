using Irony.Parsing;

namespace SqlBuildingBlocks.Grammars.MySQL;

public class SimpleId : SqlBuildingBlocks.SimpleId
{
    public SimpleId(Grammar grammar)
        : base(grammar)
    {
        Rule = MySqlTerminalFactory.CreateSqlExtIdentifier(grammar, "id_simple");
    }
}
