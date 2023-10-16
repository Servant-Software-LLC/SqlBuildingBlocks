using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class TableName : NonTerminal
{
    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public TableName(Grammar grammar) : this(grammar, new Id(grammar)) { }
    public TableName(Grammar grammar, Id id) : this(grammar, new AliasOpt(grammar, id.SimpleId), id) { }

    public TableName(Grammar grammar, AliasOpt aliasOpt, Id id)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        AliasOpt = aliasOpt ?? throw new ArgumentNullException(nameof(aliasOpt));

        // Define spaceOpt NonTerminal
        var spaceOpt = new NonTerminal("spaceOpt", grammar.Empty | " ");

        Rule = id + spaceOpt + aliasOpt;

        grammar.MarkTransient(spaceOpt);
    }

    public Id Id { get; }
    public AliasOpt AliasOpt { get; }


    public virtual SqlTable Create(ParseTreeNode tableId)
    {
        if (tableId.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {tableId.Term.Name} which does not match {TermName}", nameof(tableId));
        }

        SqlTable table = Id.CreateTable(tableId.ChildNodes[0]);

        //Check to see if a table alias was provided.
        if (tableId.ChildNodes.Count == 2 && tableId.ChildNodes[1].ChildNodes.Count == 1)
        {
            var tableAliasId = tableId.ChildNodes[1];
            table.TableAlias = AliasOpt.Create(tableAliasId);
        }

        return table;
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
