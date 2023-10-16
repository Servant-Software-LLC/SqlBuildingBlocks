using Irony.Parsing;

namespace SqlBuildingBlocks.Extensions;

public static class ParseTreeNodeListExtensions
{
    public static IEnumerable<T> Create<T>(this ParseTreeNodeList childNodes, Func<ParseTreeNode, T> createNode)
    {
        foreach (ParseTreeNode node in childNodes)
        {
            yield return createNode(node);
        }
    }
}
