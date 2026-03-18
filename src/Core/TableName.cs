using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class TableName : NonTerminal
{
    private readonly Grammar grammar;
    private const string DerivedTableTermName = "derivedTable";

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public TableName(Grammar grammar) : this(grammar, new Id(grammar)) { }
    public TableName(Grammar grammar, Id id) : this(grammar, new AliasOpt(grammar, id.SimpleId), id) { }

    public TableName(Grammar grammar, AliasOpt aliasOpt, Id id)
        : base(TermName)
    {
        this.grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
        Id = id ?? throw new ArgumentNullException(nameof(id));
        AliasOpt = aliasOpt ?? throw new ArgumentNullException(nameof(aliasOpt));

        // Define spaceOpt NonTerminal
        var spaceOpt = new NonTerminal("spaceOpt", grammar.Empty | " ");

        Rule = id + spaceOpt + aliasOpt;

        grammar.MarkTransient(spaceOpt);
    }

    public Id Id { get; }
    public AliasOpt AliasOpt { get; }

    public void InitializeRule(SelectStmt selectStmt)
    {
        var derivedTable = new NonTerminal(DerivedTableTermName);
        derivedTable.Rule = "(" + selectStmt + ")" + AliasOpt;

        Rule |= derivedTable;

        grammar.MarkPunctuation("(", ")");
    }


    public virtual SqlTable Create(ParseTreeNode tableId)
    {
        if (tableId.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {tableId.Term.Name} which does not match {TermName}", nameof(tableId));
        }

        if (tableId.ChildNodes[0].Term.Name == DerivedTableTermName)
        {
            var derivedTableNode = tableId.ChildNodes[0];
            var selectDefinition = CreateDerivedSelectDefinition(derivedTableNode.ChildNodes[0]);
            var aliasNode = derivedTableNode.ChildNodes[1];
            if (aliasNode.ChildNodes.Count == 0)
                throw new InvalidOperationException("Derived tables require an alias.");

            var alias = AliasOpt.Create(aliasNode);
            return new SqlDerivedTable(selectDefinition, alias);
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

    protected virtual SqlSelectDefinition CreateDerivedSelectDefinition(ParseTreeNode selectStmtNode)
    {
        if (grammar.Root is not SelectStmt selectStmt)
            throw new InvalidOperationException("The grammar root must be a SelectStmt to create derived table definitions.");

        return selectStmt.Create(selectStmtNode);
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
