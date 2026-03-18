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
        aggregateArg.Rule = expr | "*";

        var aggregate = new NonTerminal(sAggregate);
        aggregate.Rule = aggregateName + "(" + aggregateArg + ")";

        var columnSource = new NonTerminal("columnSource");
        columnSource.Rule = aggregate | FuncCall | Id | Id + ".*";

        var columnItem = new NonTerminal("columnItem");
        columnItem.Rule = columnSource + AliasOpt;

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

        Rule = selectCore + setOperationListOpt + orderClauseOpt;

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

        UpdateSelectCore(selectStmt.ChildNodes[0], sqlSelectDefinition);

        AddSetOperations(sqlSelectDefinition, selectStmt.ChildNodes[1]);

        //ORDER BY clause
        if (OrderByList != null)
            sqlSelectDefinition.OrderBy = OrderByList.Create(selectStmt.ChildNodes[2]);
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
        string? alias = null;
        if (columnItem.ChildNodes.Count > 1)
        {
            //TODO: Can we use AliasOpt.Create() here?

            var columnAliasId = columnItem.ChildNodes[1];
            if (columnAliasId.Token != null)
                alias = columnAliasId.Token.ValueString;
            else if (columnAliasId.ChildNodes.Count > 0)
                alias = columnAliasId.ChildNodes[0].ChildNodes[0].Token.ValueString;
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
            sqlSelectDefinition.Columns.Add(
                new SqlFunctionColumn(FuncCall!.Create(columnType))
                {
                    ColumnAlias = alias
                }
            );
            return;
        }

        if (columnType.Term.Name == sAggregate)
        {
            var aggregateName = columnType.ChildNodes[0].ChildNodes[0].Term.Name;
            var aggregateArg = columnType.ChildNodes[1].ChildNodes[0].Token.ValueString == "*" ? null : Expr!.Create(columnType.ChildNodes[1].ChildNodes[0]);
            sqlSelectDefinition.Columns.Add(
                new SqlAggregate(aggregateName, aggregateArg)
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
}
