using Irony.Parsing;

namespace SqlBuildingBlocks;

public static class Comment
{
    /// <summary>
    /// Register the non-grammar terminals that are not part of the resulting <see cref="ParseTree"/>.
    /// </summary>
    /// <param name="grammar"></param>
    public static void Register(Grammar grammar)
    {
        var comment = new CommentTerminal("comment", "/*", "*/");
        var lineComment = new CommentTerminal("line_comment", "--", "\n", "\r\n");
        grammar.NonGrammarTerminals.Add(comment);
        grammar.NonGrammarTerminals.Add(lineComment);
    }
}
