using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class SelectStmt : NonTerminal
{
    private const string sAggregate = "aggregate";
    private const string sSelectCore = "selectCore";
    private const string sSetOperationListOpt = "setOperationListOpt";
    private const string sScalarSubqueryColumnSource = "scalarSubqueryColumnSource";
    private const string sWithClauseOpt = "withClauseOpt";
    private const string sCteDefinition = "cteDefinition";
    private const string sCteDefinitionList = "cteDefinitionList";
    private const string sOverClauseOpt = "overClauseOpt";
    private const string sPartitionByOpt = "partitionByOpt";
    private const string sWindowOrderByOpt = "windowOrderByOpt";
    private const string sWindowFrameOpt = "windowFrameOpt";
    private const string sWindowFrameBound = "windowFrameBound";
    private const bool ignoreCase = true;  //Exposure of this field may come later.

    protected readonly Grammar grammar;

    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public SelectStmt(Grammar grammar) : this(grammar, new Id(grammar)) { }
    public SelectStmt(Grammar grammar, Id id) : this(grammar, id, new Expr(grammar, id), new TableName(grammar, id)) { }

    public SelectStmt(Grammar grammar, Id id, Expr expr, TableName tableName) :
        this(grammar, id, expr, new AliasOpt(grammar, id.SimpleId), tableName, new JoinChainOpt(grammar, tableName, expr), 
             new OrderByList(grammar, id), new WhereClauseOpt(grammar, expr), new FuncCall(grammar, id, expr)) { }


    public SelectStmt(Grammar grammar, Id id, Expr expr, AliasOpt aliasOpt, TableName tableName, JoinChainOpt joinChainOpt, 
                      OrderByList orderByList, WhereClauseOpt whereClauseOpt, FuncCall funcCall)
        : base(TermName)
    {
        this.grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Expr = expr ?? throw new ArgumentNullException(nameof(expr));
        AliasOpt = aliasOpt ?? throw new ArgumentNullException(nameof(aliasOpt));
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        JoinChainOpt = joinChainOpt ?? throw new ArgumentNullException(nameof(joinChainOpt));
        OrderByList = orderByList ?? throw new ArgumentNullException(nameof(orderByList));
        WhereClauseOpt = whereClauseOpt ?? throw new ArgumentNullException(nameof(whereClauseOpt));
        FuncCall = funcCall ?? throw new ArgumentNullException(nameof(funcCall));

        var SELECT = grammar.ToTerm("SELECT");
        var COUNT = grammar.ToTerm("COUNT");
        var COMMA = grammar.ToTerm(",");
        var INTO = grammar.ToTerm("INTO");
        var FROM = grammar.ToTerm("FROM");
        var BY = grammar.ToTerm("BY");
        var UNION = grammar.ToTerm("UNION");
        var ALL = grammar.ToTerm("ALL");
        var INTERSECT = grammar.ToTerm("INTERSECT");
        var EXCEPT = grammar.ToTerm("EXCEPT");

        var selRestrOpt = new NonTerminal("selRestrOpt");
        selRestrOpt.Rule = grammar.Empty | "ALL" | "DISTINCT";

        var aggregateName = new NonTerminal("aggregateName");
        aggregateName.Rule = COUNT | "Avg" | "Min" | "Max" | "StDev" | "StDevP" | "Sum" | "Var" | "VarP";

        var aggregateArg = new NonTerminal("aggregateArg");
        aggregateArg.Rule = expr | "*" | "DISTINCT" + expr;

        var aggregate = new NonTerminal(sAggregate);
        aggregate.Rule = aggregateName + "(" + aggregateArg + ")";

        var scalarSubqueryColumnSource = new NonTerminal(sScalarSubqueryColumnSource);
        scalarSubqueryColumnSource.Rule = "(" + this + ")";

        // Window function OVER clause
        var OVER = grammar.ToTerm("OVER");
        var PARTITION = grammar.ToTerm("PARTITION");
        var ROWS = grammar.ToTerm("ROWS");
        var RANGE = grammar.ToTerm("RANGE");
        var GROUPS = grammar.ToTerm("GROUPS");
        var BETWEEN_KW = grammar.ToTerm("BETWEEN");
        var UNBOUNDED = grammar.ToTerm("UNBOUNDED");
        var PRECEDING = grammar.ToTerm("PRECEDING");
        var FOLLOWING = grammar.ToTerm("FOLLOWING");
        var CURRENT = grammar.ToTerm("CURRENT");
        var ROW = grammar.ToTerm("ROW");

        var windowOrderByMember = new NonTerminal("windowOrderByMember");
        var windowOrderByDirOpt = new NonTerminal("windowOrderByDirOpt");
        windowOrderByDirOpt.Rule = grammar.Empty | "ASC" | "DESC";
        windowOrderByMember.Rule = id + windowOrderByDirOpt;

        var windowOrderByList = new NonTerminal("windowOrderByList");
        windowOrderByList.Rule = grammar.MakePlusRule(windowOrderByList, COMMA, windowOrderByMember);

        ExprList windowExprList = new(grammar, Expr);

        var partitionByOpt = new NonTerminal(sPartitionByOpt);
        partitionByOpt.Rule = grammar.Empty | PARTITION + BY + windowExprList;

        var windowOrderByOpt = new NonTerminal(sWindowOrderByOpt);
        windowOrderByOpt.Rule = grammar.Empty | "ORDER" + BY + windowOrderByList;

        var frameOffset = new NumberLiteral("frameOffset");

        var windowFrameBound = new NonTerminal(sWindowFrameBound);
        windowFrameBound.Rule = UNBOUNDED + PRECEDING
                              | UNBOUNDED + FOLLOWING
                              | CURRENT + ROW
                              | frameOffset + PRECEDING
                              | frameOffset + FOLLOWING;

        var windowFrameMode = new NonTerminal("windowFrameMode");
        windowFrameMode.Rule = ROWS | RANGE | GROUPS;

        var windowFrameOpt = new NonTerminal(sWindowFrameOpt);
        windowFrameOpt.Rule = grammar.Empty
                            | windowFrameMode + windowFrameBound
                            | windowFrameMode + BETWEEN_KW + windowFrameBound + "AND" + windowFrameBound;

        var overClauseOpt = new NonTerminal(sOverClauseOpt);
        overClauseOpt.Rule = grammar.Empty | OVER + "(" + partitionByOpt + windowOrderByOpt + windowFrameOpt + ")";

        grammar.MarkPunctuation(OVER);

        var columnSource = new NonTerminal("columnSource");
        columnSource.Rule = aggregate | FuncCall | Id | scalarSubqueryColumnSource | Id + ".*";

        var columnItem = new NonTerminal("columnItem");
        columnItem.Rule = columnSource + overClauseOpt + AliasOpt;

        var columnItemList = new NonTerminal("columnItemList");
        columnItemList.Rule = grammar.MakePlusRule(columnItemList, COMMA, columnItem);

        var selList = new NonTerminal("selList");
        selList.Rule = columnItemList | "*";

        var intoClauseOpt = new NonTerminal("intoClauseOpt");
        intoClauseOpt.Rule = grammar.Empty | INTO + Id;

        var fromClauseOpt = new NonTerminal("fromClauseOpt");
        fromClauseOpt.Rule = grammar.Empty | FROM + TableName + JoinChainOpt;

        IdList idList = new IdList(grammar, Id);

        var groupClauseOpt = new NonTerminal("groupClauseOpt");
        groupClauseOpt.Rule = grammar.Empty | "GROUP" + BY + idList;

        var havingClauseOpt = new NonTerminal("havingClauseOpt");
        havingClauseOpt.Rule = grammar.Empty | "HAVING" + expr;

        var orderClauseOpt = new NonTerminal("orderClauseOpt");
        orderClauseOpt.Rule = grammar.Empty | "ORDER" + BY + OrderByList;

        var selectCore = new NonTerminal(sSelectCore);
        selectCore.Rule = SELECT + selRestrOpt + selList + intoClauseOpt + fromClauseOpt + WhereClauseOpt +
                          groupClauseOpt + havingClauseOpt;

        var setOperator = new NonTerminal("setOperator");
        setOperator.Rule = UNION + ALL | UNION | INTERSECT | EXCEPT;

        var setOperation = new NonTerminal("setOperation");
        setOperation.Rule = setOperator + selectCore;

        var setOperationListOpt = new NonTerminal(sSetOperationListOpt);
        setOperationListOpt.Rule = grammar.MakeStarRule(setOperationListOpt, setOperation);

        var WITH = grammar.ToTerm("WITH");
        var RECURSIVE = grammar.ToTerm("RECURSIVE");
        var AS = grammar.ToTerm("AS");

        var recursiveOpt = new NonTerminal("recursiveOpt");
        recursiveOpt.Rule = grammar.Empty | RECURSIVE;

        var cteDefinition = new NonTerminal(sCteDefinition);
        cteDefinition.Rule = Id + AS + "(" + selectCore + setOperationListOpt + orderClauseOpt + ")";

        var cteDefinitionList = new NonTerminal(sCteDefinitionList);
        cteDefinitionList.Rule = grammar.MakePlusRule(cteDefinitionList, COMMA, cteDefinition);

        var withClauseOpt = new NonTerminal(sWithClauseOpt);
        withClauseOpt.Rule = grammar.Empty | WITH + recursiveOpt + cteDefinitionList;

        grammar.MarkPunctuation(WITH);

        Rule = withClauseOpt + selectCore + setOperationListOpt + orderClauseOpt;

        grammar.MarkPunctuation("(", ")");

        TableName.InitializeRule(this);
    }

    public Id Id { get; }
    public Expr Expr { get; }
    public AliasOpt AliasOpt { get; }
    public TableName TableName { get; }
    public JoinChainOpt JoinChainOpt { get; }
    public OrderByList OrderByList { get; }
    public WhereClauseOpt WhereClauseOpt { get; }
    public FuncCall FuncCall { get; }

    public virtual SqlSelectDefinition Create(ParseTreeNode selectStmt)
    {
        SqlSelectDefinition sqlSelectDefinition = new();
        Update(selectStmt, sqlSelectDefinition);
        return sqlSelectDefinition;
    }

    public virtual SqlSelectDefinition Create(ParseTreeNode selectStmt, IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider, 
                                              IFunctionProvider? functionProvider = null)
    {
        var sqlSelectDefinition = Create(selectStmt);
        
        sqlSelectDefinition.ResolveReferences(databaseConnectionProvider, tableSchemaProvider, functionProvider);

        return sqlSelectDefinition;
    }

    public virtual void Update(ParseTreeNode selectStmt, SqlSelectDefinition sqlSelectDefinition)
    {
        if (selectStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {selectStmt.Term.Name} which does not match {TermName}", nameof(selectStmt));
        }

        //WITH clause (CTEs)
        AddCtes(sqlSelectDefinition, selectStmt.ChildNodes[0]);

        UpdateSelectCore(selectStmt.ChildNodes[1], sqlSelectDefinition);

        AddSetOperations(sqlSelectDefinition, selectStmt.ChildNodes[2]);

        //ORDER BY clause
        if (OrderByList != null)
            sqlSelectDefinition.OrderBy = OrderByList.Create(selectStmt.ChildNodes[3]);
    }

    protected virtual void UpdateSelectCore(ParseTreeNode selectCore, SqlSelectDefinition sqlSelectDefinition)
    {
        if (selectCore.Term.Name != sSelectCore)
            throw new ArgumentException($"Expected a '{sSelectCore}' node but received '{selectCore.Term.Name}'.", nameof(selectCore));

        var selList = selectCore.ChildNodes[2];
        if (selList.Term.Name != nameof(selList))
            throw new Exception($"The {nameof(selectCore)} provided to the ctor of {nameof(SqlSelectDefinition)} did not have a {nameof(selList)} as its third child node.");

        AddColumns(sqlSelectDefinition, selList);

        var fromClauseOpt = selectCore.ChildNodes[4];
        if (fromClauseOpt.Term.Name == nameof(fromClauseOpt))
            AddTables(sqlSelectDefinition, fromClauseOpt);

        sqlSelectDefinition.WhereClause = WhereClauseOpt?.Create(selectCore.ChildNodes[5]);
    }

    protected virtual void AddSetOperations(SqlSelectDefinition sqlSelectDefinition, ParseTreeNode setOperationListOpt)
    {
        if (setOperationListOpt.Term.Name != sSetOperationListOpt)
            throw new ArgumentException($"Expected a '{sSetOperationListOpt}' node but received '{setOperationListOpt.Term.Name}'.", nameof(setOperationListOpt));

        foreach (var setOperationNode in setOperationListOpt.ChildNodes)
        {
            var rightSelect = new SqlSelectDefinition();
            UpdateSelectCore(setOperationNode.ChildNodes[1], rightSelect);
            sqlSelectDefinition.SetOperations.Add(new SqlSetOperation(CreateSetOperator(setOperationNode.ChildNodes[0]), rightSelect));
        }
    }

    protected virtual void AddCtes(SqlSelectDefinition sqlSelectDefinition, ParseTreeNode withClauseOpt)
    {
        if (withClauseOpt.Term.Name != sWithClauseOpt)
            throw new ArgumentException($"Expected a '{sWithClauseOpt}' node but received '{withClauseOpt.Term.Name}'.", nameof(withClauseOpt));

        // Empty WITH clause (no CTEs)
        if (withClauseOpt.ChildNodes.Count == 0)
            return;

        // Child 0 is recursiveOpt, Child 1 is cteDefinitionList
        var recursiveOptNode = withClauseOpt.ChildNodes[0];
        bool isRecursive = recursiveOptNode.ChildNodes.Count > 0;

        var cteDefinitionList = withClauseOpt.ChildNodes[1];
        foreach (var cteNode in cteDefinitionList.ChildNodes)
        {
            var cteName = Id.GetColumnBaseValues(cteNode.ChildNodes[0]).ColumnName;

            // Build the CTE's select definition from its selectCore + setOperationListOpt + orderClauseOpt
            var cteSelectDefinition = new SqlSelectDefinition();
            UpdateSelectCore(cteNode.ChildNodes[1], cteSelectDefinition);
            AddSetOperations(cteSelectDefinition, cteNode.ChildNodes[2]);
            if (OrderByList != null)
                cteSelectDefinition.OrderBy = OrderByList.Create(cteNode.ChildNodes[3]);

            sqlSelectDefinition.Ctes.Add(new SqlCteDefinition(cteName, cteSelectDefinition, isRecursive));
        }
    }

    protected virtual SqlSetOperator CreateSetOperator(ParseTreeNode setOperatorNode)
    {
        var symbol = string.Join(" ", setOperatorNode.ChildNodes.Select(child => child.Token?.Text?.ToUpperInvariant()));

        return symbol switch
        {
            "UNION" => SqlSetOperator.Union,
            "UNION ALL" => SqlSetOperator.UnionAll,
            "INTERSECT" => SqlSetOperator.Intersect,
            "EXCEPT" => SqlSetOperator.Except,
            _ => throw new ArgumentException($"Unsupported set operator '{symbol}'.", nameof(setOperatorNode)),
        };
    }


    protected SqlColumn CreateColumn(ParseTreeNode columnId, string? alias)
    {
        var column = Id!.CreateColumn(columnId);
        if (column == null)
            throw new Exception($"Trying to CreateColumn returned null.");

        column.ColumnAlias = alias;
        return column;
    }

    protected virtual void AddColumns(SqlSelectDefinition sqlSelectDefinition, ParseTreeNode selList)
    {
        if (selList.ChildNodes.Count == 1 && selList.ChildNodes[0].Token != null)
        {
            var keySymbol = selList.ChildNodes[0].Token.Text;
            if (keySymbol != "*")
                throw new ArgumentException($"Expected the keySymbol in the {nameof(SqlColumn)} ctor to be a '*'");

            sqlSelectDefinition.Columns.Add(new SqlAllColumns());
            return;
        }

        var columnItemList = selList.ChildNodes[0];
        foreach (var columnItem in columnItemList.ChildNodes)
        {
            AddColumn(sqlSelectDefinition, columnItem);
        }

    }

    protected virtual void AddColumn(SqlSelectDefinition sqlSelectDefinition, ParseTreeNode columnItem)
    {
        var columnSource = columnItem.ChildNodes[0];

        //Is this an all column for just one table.
        if (columnSource.ChildNodes.Count > 1)
        {
            var columnId = columnSource.ChildNodes[0];
            var tableName = columnId.ChildNodes[0].ChildNodes[0].Token.Text;
            sqlSelectDefinition.Columns.Add(new SqlAllColumns() { TableName = tableName });
            return;
        }

        //Columns (Column, Aggregate, Function) in the SELECT can have aliases
        // columnItem children: [columnSource, overClauseOpt, aliasOpt]
        string? alias = null;
        if (columnItem.ChildNodes.Count > 2)
        {
            //TODO: Can we use AliasOpt.Create() here?

            var columnAliasId = columnItem.ChildNodes[2];
            if (columnAliasId.Token != null)
                alias = columnAliasId.Token.ValueString;
            else if (columnAliasId.ChildNodes.Count > 0)
                alias = columnAliasId.ChildNodes[0].ChildNodes[0].Token.ValueString;
        }

        // Extract the optional OVER clause (at index 1 of columnItem)
        SqlWindowSpecification? windowSpec = null;
        if (columnItem.ChildNodes.Count > 1)
        {
            var overClauseOptNode = columnItem.ChildNodes[1];
            if (overClauseOptNode.Term.Name == sOverClauseOpt)
                windowSpec = CreateWindowSpecification(overClauseOptNode);
        }

        var columnType = columnSource.ChildNodes[0];
        if (columnType.Term.Name == Id.TermName)
        {
            SqlColumn column = CreateColumn(columnType, alias);
            sqlSelectDefinition.Columns.Add(column);
            return;
        }

        if (columnType.Term.Name == FuncCall.TermName)
        {
            var function = FuncCall!.Create(columnType);
            function.WindowSpecification = windowSpec;

            sqlSelectDefinition.Columns.Add(
                new SqlFunctionColumn(function)
                {
                    ColumnAlias = alias
                }
            );
            return;
        }

        if (columnType.Term.Name == sAggregate)
        {
            var aggregateName = columnType.ChildNodes[0].ChildNodes[0].Term.Name;

            var aggregateArgNode = columnType.ChildNodes[1];
            bool isDistinct = aggregateArgNode.ChildNodes.Count == 2;

            // When DISTINCT is present: children are [DISTINCT-token, expr]
            // When not present: children are [expr] or [*-token]
            var argChild = isDistinct ? aggregateArgNode.ChildNodes[1] : aggregateArgNode.ChildNodes[0];
            var aggregateArg = argChild.Token?.ValueString == "*" ? null : Expr!.Create(argChild);
            var aggregate = new SqlAggregate(aggregateName, aggregateArg)
            {
                IsDistinct = isDistinct,
                ColumnAlias = alias,
                WindowSpecification = windowSpec
            };

            sqlSelectDefinition.Columns.Add(aggregate);
            return;
        }

        if (columnType.Term.Name == sScalarSubqueryColumnSource)
        {
            sqlSelectDefinition.Columns.Add(
                new SqlScalarSubqueryColumn(Create(columnType.ChildNodes[0]))
                {
                    ColumnAlias = alias
                }
            );
            return;
        }

        throw new Exception($"Column type {columnType.Term.Name} not supported in SQL parsing.");
    }

    protected virtual void AddTables(SqlSelectDefinition sqlSelectDefinition, ParseTreeNode fromClauseOpt)
    {
        if (fromClauseOpt.ChildNodes.Count < 2)
            return;

        AddTable(sqlSelectDefinition, fromClauseOpt);
        AddJoins(sqlSelectDefinition, fromClauseOpt);
    }


    protected virtual void AddTable(SqlSelectDefinition sqlSelectDefinition, ParseTreeNode fromClauseOpt)
    {
        var tableNameNode = fromClauseOpt.ChildNodes[1];
        sqlSelectDefinition.Table = TableName!.Create(tableNameNode);
    }

    protected virtual void AddJoins(SqlSelectDefinition sqlSelectDefinition, ParseTreeNode fromClauseOpt)
    {
        var joinChainOptNode = fromClauseOpt.ChildNodes[2];
        if (joinChainOptNode.ChildNodes.Count == 0)
            return;

        var sqlJoins = JoinChainOpt!.Create(joinChainOptNode);

        foreach (SqlJoin sqlJoin in sqlJoins)
            sqlSelectDefinition.Joins.Add(sqlJoin);
    }

    /// <summary>
    /// Creates a <see cref="SqlWindowSpecification"/> from an overClauseOpt parse tree node.
    /// Returns null if the OVER clause is absent (grammar.Empty matched).
    /// </summary>
    protected virtual SqlWindowSpecification? CreateWindowSpecification(ParseTreeNode overClauseOpt)
    {
        if (overClauseOpt.Term.Name != sOverClauseOpt)
            throw new ArgumentException($"Expected a '{sOverClauseOpt}' node but received '{overClauseOpt.Term.Name}'.", nameof(overClauseOpt));

        // Empty OVER clause (grammar.Empty matched)
        if (overClauseOpt.ChildNodes.Count == 0)
            return null;

        var windowSpec = new SqlWindowSpecification();

        // overClauseOpt → OVER + "(" + partitionByOpt + windowOrderByOpt + windowFrameOpt + ")"
        // After punctuation stripping: [partitionByOpt, windowOrderByOpt, windowFrameOpt]
        var partitionByOptNode = overClauseOpt.ChildNodes[0];
        var windowOrderByOptNode = overClauseOpt.ChildNodes[1];
        var windowFrameOptNode = overClauseOpt.ChildNodes[2];

        // PARTITION BY
        if (partitionByOptNode.Term.Name == sPartitionByOpt && partitionByOptNode.ChildNodes.Count > 0)
        {
            // partitionByOpt → PARTITION + BY + exprList
            // After consuming PARTITION and BY terminals, the exprList is the remaining child
            var exprListNode = partitionByOptNode.ChildNodes[partitionByOptNode.ChildNodes.Count - 1];
            foreach (var exprNode in exprListNode.ChildNodes)
            {
                windowSpec.PartitionBy.Add(Expr!.Create(exprNode));
            }
        }

        // ORDER BY
        if (windowOrderByOptNode.Term.Name == sWindowOrderByOpt && windowOrderByOptNode.ChildNodes.Count > 0)
        {
            // windowOrderByOpt → "ORDER" + BY + windowOrderByList
            var orderByListNode = windowOrderByOptNode.ChildNodes[windowOrderByOptNode.ChildNodes.Count - 1];
            foreach (var orderByMember in orderByListNode.ChildNodes)
            {
                // windowOrderByMember → id + windowOrderByDirOpt
                var idNode = orderByMember.ChildNodes[0];
                var dirNode = orderByMember.ChildNodes[1];

                var columnBaseValues = Id.GetColumnBaseValues(idNode);
                string columnName = columnBaseValues.TableName != null
                    ? $"{columnBaseValues.TableName}.{columnBaseValues.ColumnName}"
                    : columnBaseValues.ColumnName;

                bool descending = dirNode.ChildNodes.Count > 0 &&
                                  string.Equals(dirNode.ChildNodes[0].Term.Name, "DESC", StringComparison.OrdinalIgnoreCase);

                windowSpec.OrderBy.Add(new SqlOrderByColumn(columnName, descending));
            }
        }

        // Window frame
        if (windowFrameOptNode.Term.Name == sWindowFrameOpt && windowFrameOptNode.ChildNodes.Count > 0)
        {
            var frameModeNode = windowFrameOptNode.ChildNodes[0];
            var frameMode = frameModeNode.ChildNodes[0].Term.Name.ToUpperInvariant() switch
            {
                "ROWS" => WindowFrameMode.Rows,
                "RANGE" => WindowFrameMode.Range,
                "GROUPS" => WindowFrameMode.Groups,
                _ => throw new ArgumentException($"Unsupported window frame mode '{frameModeNode.ChildNodes[0].Term.Name}'.")
            };

            if (windowFrameOptNode.ChildNodes.Count == 2)
            {
                // windowFrameMode + windowFrameBound (no BETWEEN)
                var startBound = CreateWindowFrameBound(windowFrameOptNode.ChildNodes[1]);
                windowSpec.Frame = new SqlWindowFrame(frameMode, startBound);
            }
            else
            {
                // windowFrameMode + BETWEEN + windowFrameBound + AND + windowFrameBound
                // After punctuation stripping for BETWEEN and AND... let's check
                // Actually BETWEEN and AND are NOT marked as punctuation here, they stay as tokens
                // So children: [frameModeNode, BETWEEN, startBound, AND, endBound]
                var startBound = CreateWindowFrameBound(windowFrameOptNode.ChildNodes[2]);
                var endBound = CreateWindowFrameBound(windowFrameOptNode.ChildNodes[4]);
                windowSpec.Frame = new SqlWindowFrame(frameMode, startBound, endBound);
            }
        }

        return windowSpec;
    }

    /// <summary>
    /// Creates a <see cref="SqlWindowFrameBound"/> from a windowFrameBound parse tree node.
    /// </summary>
    protected virtual SqlWindowFrameBound CreateWindowFrameBound(ParseTreeNode boundNode)
    {
        if (boundNode.Term.Name != sWindowFrameBound)
            throw new ArgumentException($"Expected a '{sWindowFrameBound}' node but received '{boundNode.Term.Name}'.", nameof(boundNode));

        // Determine the bound type from child tokens
        var firstChild = boundNode.ChildNodes[0];
        var secondChild = boundNode.ChildNodes.Count > 1 ? boundNode.ChildNodes[1] : null;

        var firstText = firstChild.Token?.Text?.ToUpperInvariant() ?? firstChild.Term.Name.ToUpperInvariant();

        if (firstText == "CURRENT")
            return new SqlWindowFrameBound(WindowFrameBoundType.CurrentRow);

        if (firstText == "UNBOUNDED")
        {
            var dirText = secondChild?.Token?.Text?.ToUpperInvariant() ?? secondChild?.Term.Name.ToUpperInvariant();
            return dirText == "PRECEDING"
                ? new SqlWindowFrameBound(WindowFrameBoundType.UnboundedPreceding)
                : new SqlWindowFrameBound(WindowFrameBoundType.UnboundedFollowing);
        }

        // Numeric offset: frameOffset + PRECEDING/FOLLOWING
        var offset = Convert.ToInt32(firstChild.Token.Value);
        var direction = secondChild?.Token?.Text?.ToUpperInvariant() ?? secondChild?.Term.Name.ToUpperInvariant();
        return direction == "PRECEDING"
            ? new SqlWindowFrameBound(WindowFrameBoundType.Preceding, offset)
            : new SqlWindowFrameBound(WindowFrameBoundType.Following, offset);
    }
}
