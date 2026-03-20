using Irony.Parsing;
using SqlBuildingBlocks.LogicalEntities;

namespace SqlBuildingBlocks.Grammars.MySQL;

public class SelectStmt : SqlBuildingBlocks.SelectStmt
{
    private const string sWithRollupOpt = "withRollupOpt";

    private LimitOffsetClauseOpt? limitOffsetClauseOpt;

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor.
    /// </summary>
    /// <param name="grammar"></param>
    public SelectStmt(Grammar grammar) : base(grammar)
    {
        AddWithRollupSupport(grammar);
        limitOffsetClauseOpt = new(grammar);
        Rule += limitOffsetClauseOpt;
    }

    public SelectStmt(Grammar grammar, Id id) : base(grammar, id)
    {
        AddWithRollupSupport(grammar);
        limitOffsetClauseOpt = new(grammar);
        Rule += limitOffsetClauseOpt;
    }

    public SelectStmt(Grammar grammar, Id id, SqlBuildingBlocks.Expr expr, TableName tableName)
        : base(grammar, id, expr, tableName)
    {
        AddWithRollupSupport(grammar);
        limitOffsetClauseOpt = new(grammar);
        Rule += limitOffsetClauseOpt;
    }

    public SelectStmt(Grammar grammar, Id id, Expr expr, AliasOpt aliasOpt, TableName tableName,
                      JoinChainOpt joinChainOpt, OrderByList orderByList, WhereClauseOpt whereClauseOpt, FuncCall funcCall)
        : base(grammar, id, expr, aliasOpt, tableName, joinChainOpt, orderByList, whereClauseOpt, funcCall)
    {
        AddWithRollupSupport(grammar);
        limitOffsetClauseOpt = new(grammar);
        Rule += limitOffsetClauseOpt;
    }

    public override SqlSelectDefinition Create(ParseTreeNode selectStmt)
    {
        var sqlSelectDefinition = base.Create(selectStmt);

        if (selectStmt.ChildNodes.Count > 4)
            sqlSelectDefinition.Limit = limitOffsetClauseOpt!.Create(selectStmt.ChildNodes[4]);

        return sqlSelectDefinition;
    }

    protected override SqlGroupByClause CreateGroupByClause(ParseTreeNode groupClauseOptNode)
    {
        // In MySQL grammar, groupClauseOpt children: [GROUP, BY, groupByElementList, withRollupOpt]
        var lastChild = groupClauseOptNode.ChildNodes[groupClauseOptNode.ChildNodes.Count - 1];
        bool hasWithRollup = lastChild.Term.Name == sWithRollupOpt && lastChild.ChildNodes.Count > 0;

        if (!hasWithRollup)
        {
            // withRollupOpt is empty but still present as last child — adjust by
            // removing it temporarily so the base class finds groupByElementList as last child.
            groupClauseOptNode.ChildNodes.RemoveAt(groupClauseOptNode.ChildNodes.Count - 1);
            try
            {
                return base.CreateGroupByClause(groupClauseOptNode);
            }
            finally
            {
                groupClauseOptNode.ChildNodes.Add(lastChild);
            }
        }

        // WITH ROLLUP: all plain columns become a single ROLLUP grouping set
        var groupBy = new SqlGroupByClause();
        var groupByElementList = groupClauseOptNode.ChildNodes[groupClauseOptNode.ChildNodes.Count - 2];
        var rollupSet = new SqlGroupingSet(GroupingSetType.Rollup);

        foreach (var elementNode in groupByElementList.ChildNodes)
        {
            var child = elementNode.ChildNodes[0];
            if (child.Term.Name == Id.TermName)
            {
                var column = Id.CreateColumn(child);
                string columnName = column.TableName != null
                    ? $"{column.TableName}.{column.ColumnName}"
                    : column.ColumnName;
                rollupSet.Sets.Add(new List<string> { columnName });
            }
        }

        groupBy.GroupingSets.Add(rollupSet);
        return groupBy;
    }

    private void AddWithRollupSupport(Grammar grammar)
    {
        // MySQL supports GROUP BY col1, col2 WITH ROLLUP syntax
        var withRollupOpt = new NonTerminal(sWithRollupOpt);
        withRollupOpt.Rule = grammar.Empty | grammar.ToTerm("WITH") + grammar.ToTerm("ROLLUP");

        GroupClauseOpt.Rule = grammar.Empty
            | grammar.ToTerm("GROUP") + grammar.ToTerm("BY") + GroupByElementList + withRollupOpt;
    }
}
