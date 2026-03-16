using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class OrderByList : NonTerminal
{
    private const string sOrderClauseOpt = "orderClauseOpt";

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor.
    /// </summary>
    /// <param name="grammar"></param>
    public OrderByList(Grammar grammar) : this(grammar, new Id(grammar)) { }

    public OrderByList(Grammar grammar, Id id)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));

        var COMMA = grammar.ToTerm(",");

        var orderByDirOpt = new NonTerminal("orderDirOpt");
        orderByDirOpt.Rule = grammar.Empty | "ASC" | "DESC";

        var orderByMember = new NonTerminal("orderByMember");
        orderByMember.Rule = id + orderByDirOpt;

        Rule = grammar.MakePlusRule(this, COMMA, orderByMember);
    }

    public Id Id { get; }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    /// <summary>
    /// Converts an <c>orderClauseOpt</c> parse-tree node into a list of <see cref="SqlOrderByColumn"/>.
    /// Returns an empty list when the clause is absent.
    /// </summary>
    /// <param name="orderClauseOpt">The <c>orderClauseOpt</c> child node of a <c>selectStmt</c> node.</param>
    public virtual IList<SqlOrderByColumn> Create(ParseTreeNode orderClauseOpt)
    {
        if (orderClauseOpt.Term.Name != sOrderClauseOpt)
            throw new ArgumentException(
                $"Expected a '{sOrderClauseOpt}' node but received '{orderClauseOpt.Term.Name}'.",
                nameof(orderClauseOpt));

        // Empty ORDER BY clause (grammar.Empty matched)
        if (orderClauseOpt.ChildNodes.Count == 0)
            return new List<SqlOrderByColumn>();

        // orderClauseOpt → "ORDER" + BY + orderByList  (children [0]=ORDER, [1]=BY, [2]=orderByList)
        var orderByListNode = orderClauseOpt.ChildNodes[2];

        var result = new List<SqlOrderByColumn>();
        foreach (var orderByMember in orderByListNode.ChildNodes)
        {
            // orderByMember → id + orderDirOpt
            var idNode = orderByMember.ChildNodes[0];
            var dirNode = orderByMember.ChildNodes[1];

            var columnBaseValues = Id.GetColumnBaseValues(idNode);

            // Build the column name the same way GetOutputColumnName does:
            // prefer table-qualified form so callers can strip it if needed.
            string columnName = columnBaseValues.TableName != null
                ? $"{columnBaseValues.TableName}.{columnBaseValues.ColumnName}"
                : columnBaseValues.ColumnName;

            bool descending = dirNode.ChildNodes.Count > 0 &&
                              string.Equals(dirNode.ChildNodes[0].Term.Name, "DESC", StringComparison.OrdinalIgnoreCase);

            result.Add(new SqlOrderByColumn(columnName, descending));
        }

        return result;
    }
}
