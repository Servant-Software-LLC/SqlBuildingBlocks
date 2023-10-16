using Irony.Parsing;

namespace SqlBuildingBlocks.Grammars.SQLServer;

public class SimpleId : SqlBuildingBlocks.SimpleId
{
    public SimpleId(Grammar grammar)
        : base(grammar)
    {
        //Allows for normal identifiers (abc) and quoted id's ([abc d], "abc d")
        Rule = TerminalFactory.CreateSqlExtIdentifier(grammar, "id_simple");
    }
}
