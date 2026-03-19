using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class UpdateStmt : NonTerminal
{
    private const string UpdateSourceOptTermName = "updateSourceOpt";

    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor.
    /// </summary>
    /// <param name="grammar"></param>
    public UpdateStmt(Grammar grammar) : this(grammar, new Id(grammar)) { }
    public UpdateStmt(Grammar grammar, Id id) :
        this(grammar, new TableName(grammar, id), new FuncCall(grammar, id), new WhereClauseOpt(grammar, id), new JoinChainOpt(grammar, new TableName(grammar, id), new Expr(grammar, id)))  { }
    public UpdateStmt(Grammar grammar, SelectStmt selectStmt) : this(grammar, selectStmt.TableName, selectStmt.FuncCall, selectStmt.WhereClauseOpt, selectStmt.JoinChainOpt) { }
    public UpdateStmt(Grammar grammar, TableName tableName, FuncCall funcCall, WhereClauseOpt whereClauseOpt) :
        this(grammar, tableName, funcCall, whereClauseOpt, new JoinChainOpt(grammar, tableName, whereClauseOpt.Expr)) { }
    public UpdateStmt(Grammar grammar, TableName tableName, FuncCall funcCall, WhereClauseOpt whereClauseOpt, JoinChainOpt joinChainOpt) :
        this(grammar, whereClauseOpt.Expr, funcCall, tableName, whereClauseOpt, new(grammar, whereClauseOpt.Expr.Id), joinChainOpt) { }

    /// <summary>
    /// Backward-compatible constructors that accept individual Id, LiteralValue, Parameter components.
    /// These delegate to the Expr-based constructor using the Expr from WhereClauseOpt.
    /// </summary>
    public UpdateStmt(Grammar grammar, Id id, LiteralValue literalValue, Parameter parameter, FuncCall funcCall, TableName tableName, WhereClauseOpt whereClauseOpt) :
        this(grammar, whereClauseOpt.Expr, funcCall, tableName, whereClauseOpt, new(grammar, id), new JoinChainOpt(grammar, tableName, whereClauseOpt.Expr)) { }
    public UpdateStmt(Grammar grammar, Id id, LiteralValue literalValue, Parameter parameter, FuncCall funcCall, TableName tableName, WhereClauseOpt whereClauseOpt, JoinChainOpt joinChainOpt) :
        this(grammar, whereClauseOpt.Expr, funcCall, tableName, whereClauseOpt, new(grammar, id), joinChainOpt) { }
    public UpdateStmt(Grammar grammar, Id id, LiteralValue literalValue, Parameter parameter,
                      FuncCall funcCall, TableName tableName, WhereClauseOpt whereClauseOpt,
                      ReturningClauseOpt returningClauseOpt, JoinChainOpt joinChainOpt)
        : this(grammar, whereClauseOpt.Expr, funcCall, tableName, whereClauseOpt, returningClauseOpt, joinChainOpt) { }

    public UpdateStmt(Grammar grammar, Expr expr, FuncCall funcCall, TableName tableName,
                      WhereClauseOpt whereClauseOpt, ReturningClauseOpt returningClauseOpt,
                      JoinChainOpt joinChainOpt)
        : base(TermName)
    {
        Expr = expr ?? throw new ArgumentNullException(nameof(expr));
        FuncCall = funcCall ?? throw new ArgumentNullException(nameof(funcCall));
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        WhereClauseOpt = whereClauseOpt ?? throw new ArgumentNullException(nameof(whereClauseOpt));
        ReturningClauseOpt = returningClauseOpt ?? throw new ArgumentNullException(nameof(returningClauseOpt));
        JoinChainOpt = joinChainOpt ?? throw new ArgumentNullException(nameof(joinChainOpt));

        var UPDATE = grammar.ToTerm("UPDATE");
        var SET = grammar.ToTerm("SET");
        var COMMA = grammar.ToTerm(",");
        var FROM = grammar.ToTerm("FROM");

        var assignment = new NonTerminal("assignment");
        assignment.Rule = expr.Id + "=" + expr;

        var assignList = new NonTerminal("assignList");
        assignList.Rule = grammar.MakePlusRule(assignList, COMMA, assignment);

        var updateSourceOpt = new NonTerminal(UpdateSourceOptTermName);
        updateSourceOpt.Rule = grammar.Empty | FROM + tableName + JoinChainOpt;


        Rule = UPDATE + tableName + JoinChainOpt + SET + assignList + updateSourceOpt + whereClauseOpt + returningClauseOpt;
    }

    public Expr Expr { get; }
    public FuncCall FuncCall { get; }
    public TableName TableName { get; }
    public JoinChainOpt JoinChainOpt { get; }
    public WhereClauseOpt WhereClauseOpt { get; }
    public ReturningClauseOpt ReturningClauseOpt { get; }

    /// <summary>
    /// Backward-compatible properties derived from Expr.
    /// </summary>
    public Id Id => Expr.Id;
    public LiteralValue LiteralValue => Expr.LiteralValue;
    public Parameter Parameter => Expr.Parameter;

    public virtual SqlUpdateDefinition Create(ParseTreeNode updateStmt)
    {
        SqlUpdateDefinition sqlUpdateDefinition = new();
        Update(updateStmt, sqlUpdateDefinition);

        return sqlUpdateDefinition;
    }

    /// <summary>
    /// Provides a means for consumers to provide their own derived types of <see cref="SqlUpdateDefinition"/>
    /// </summary>
    /// <param name="updateStmt"></param>
    /// <param name="sqlUpdateDefinition"></param>
    public virtual void Update(ParseTreeNode updateStmt, SqlUpdateDefinition sqlUpdateDefinition)
    {
        if (updateStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {updateStmt.Term.Name} which does not match {TermName}", nameof(updateStmt));
        }

        sqlUpdateDefinition.Table = TableName.Create(updateStmt.ChildNodes[1]);

        AddJoins(sqlUpdateDefinition, updateStmt.ChildNodes[2]);

        var assignList = updateStmt.ChildNodes[4];
        foreach (ParseTreeNode assignment in assignList.ChildNodes)
        {
            var sqlColumn = Id.CreateColumn(assignment.ChildNodes[0]);
            SqlAssignment sqlAssignment = CreateAssignment(sqlColumn, assignment.ChildNodes[2]);

            sqlUpdateDefinition.Assignments.Add(sqlAssignment);
        }

        AddSource(sqlUpdateDefinition, updateStmt.ChildNodes[5]);

        sqlUpdateDefinition.WhereClause = WhereClauseOpt.Create(updateStmt.ChildNodes[6]);

        var returningClause = updateStmt.ChildNodes[7];
        sqlUpdateDefinition.Returning = ReturningClauseOpt.Create(returningClause);
    }

    protected virtual void AddJoins(SqlUpdateDefinition sqlUpdateDefinition, ParseTreeNode joinChainOptNode)
    {
        foreach (var join in JoinChainOpt.Create(joinChainOptNode))
        {
            sqlUpdateDefinition.Joins.Add(join);
        }
    }

    protected virtual void AddSource(SqlUpdateDefinition sqlUpdateDefinition, ParseTreeNode updateSourceOptNode)
    {
        if (updateSourceOptNode.Term.Name != UpdateSourceOptTermName || updateSourceOptNode.ChildNodes.Count == 0)
            return;

        sqlUpdateDefinition.SourceTable = TableName.Create(updateSourceOptNode.ChildNodes[1]);
        AddJoins(sqlUpdateDefinition, updateSourceOptNode.ChildNodes[2]);
    }

    protected virtual SqlAssignment CreateAssignment(SqlColumn sqlColumn, ParseTreeNode assignmentValue)
    {
        var sqlExpression = Expr.Create(assignmentValue);
        return new(sqlColumn, sqlExpression);
    }
}
