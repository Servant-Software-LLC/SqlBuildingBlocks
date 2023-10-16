using Irony.Parsing;

namespace SqlBuildingBlocks.Grammars.MySQL;

public class MySqlTerminalFactory
{
    public static IdentifierTerminal CreateSqlExtIdentifier(Grammar grammar, string name)
    {
        IdentifierTerminal identifierTerminal = new IdentifierTerminal(name);
        StringLiteral stringLiteral = new StringLiteral(name + "_qouted");
        stringLiteral.AddStartEnd("`", StringOptions.NoEscapes);
        stringLiteral.SetOutputTerminal(grammar, identifierTerminal);
        return identifierTerminal;
    }

    public static IdentifierTerminal CreateAliasIdentifier(Grammar grammar, string name)
    {
        IdentifierTerminal identifierTerminal = new IdentifierTerminal(name);
        StringLiteral stringLiteral = new StringLiteral(name + "_qouted");
        stringLiteral.AddStartEnd("'", StringOptions.NoEscapes);
        stringLiteral.AddStartEnd("\"", StringOptions.NoEscapes);
        stringLiteral.SetOutputTerminal(grammar, identifierTerminal);
        return identifierTerminal;
    }

}
