using Irony.Parsing;
using Xunit;

namespace SqlBuildingBlocks.IntegrationTests.Infrastructure;

/// <summary>
/// Wraps Irony parsing with assertion-based error reporting so a failure surfaces
/// as a clear test message instead of a NullReferenceException downstream.
/// </summary>
internal static class ParseHelper
{
    public static ParseTreeNode Parse(Grammar grammar, string sql)
    {
        var parseTree = ParseTree(grammar, sql);
        Assert.False(parseTree.HasErrors(),
            "Parse failed:" + Environment.NewLine +
            string.Join(Environment.NewLine, parseTree.ParserMessages.Select(m => $"  Location={m.Location} Message: {m.Message}")));
        return parseTree.Root;
    }

    public static ParseTree ParseTree(Grammar grammar, string sql)
    {
        var language = new LanguageData(grammar);
        Assert.False(language.Errors.Any(),
            "Grammar build errors:" + Environment.NewLine +
            string.Join(Environment.NewLine, language.Errors.Select(e => e.ToString())));

        var parser = new Parser(language);
        return parser.Parse(sql);
    }
}
