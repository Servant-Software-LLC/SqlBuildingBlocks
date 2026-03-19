using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class DeleteStmt : NonTerminal
{
    private const string DeleteTargetOptTermName = "deleteTargetOpt";

    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public DeleteStmt(Grammar grammar) : this(grammar, new Id(grammar)) { }
    public DeleteStmt(Grammar grammar, Id id) : this(grammar, new TableName(grammar, id), new WhereClauseOpt(grammar, id), new ReturningClauseOpt(grammar, id), new JoinChainOpt(grammar, new TableName(grammar, id), new Expr(grammar, id))) { }
    public DeleteStmt(Grammar grammar, SelectStmt selectStmt) : this(grammar, selectStmt.TableName, selectStmt.WhereClauseOpt, new ReturningClauseOpt(grammar, selectStmt.TableName.Id), selectStmt.JoinChainOpt) { }

    public DeleteStmt(Grammar sqlGrammar, TableName tableName, WhereClauseOpt whereClauseOpt, ReturningClauseOpt returningClauseOpt)
        : this(sqlGrammar, tableName, whereClauseOpt, returningClauseOpt, new JoinChainOpt(sqlGrammar, tableName, whereClauseOpt.Expr)) { }

    public DeleteStmt(Grammar sqlGrammar, TableName tableName, WhereClauseOpt whereClauseOpt, ReturningClauseOpt returningClauseOpt, JoinChainOpt joinChainOpt)
        : base(TermName)
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        WhereClauseOpt = whereClauseOpt ?? throw new ArgumentNullException(nameof(whereClauseOpt));
        ReturningClauseOpt = returningClauseOpt ?? throw new ArgumentNullException(nameof(returningClauseOpt));
        JoinChainOpt = joinChainOpt ?? throw new ArgumentNullException(nameof(joinChainOpt));

        var DELETE = sqlGrammar.ToTerm("DELETE");
        var FROM = sqlGrammar.ToTerm("FROM");

        var deleteTargetOpt = new NonTerminal(DeleteTargetOptTermName);
        deleteTargetOpt.Rule = sqlGrammar.Empty | tableName;

        Rule = DELETE + deleteTargetOpt + FROM + tableName + joinChainOpt + whereClauseOpt + returningClauseOpt;
    }

    public TableName TableName { get; }
    public JoinChainOpt JoinChainOpt { get; }
    public WhereClauseOpt WhereClauseOpt { get; }
    public ReturningClauseOpt ReturningClauseOpt { get; }

    public virtual SqlDeleteDefinition Create(ParseTreeNode deleteStmt)
    {
        SqlDeleteDefinition sqlDeleteDefinition = new();
        Update(deleteStmt, sqlDeleteDefinition);

        return sqlDeleteDefinition;
    }
    
    public virtual void Update(ParseTreeNode deleteStmt, SqlDeleteDefinition sqlDeleteDefinition)
    {
        if (deleteStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {deleteStmt.Term.Name} which does not match {TermName}", nameof(deleteStmt));
        }

        var deleteTargetOpt = deleteStmt.ChildNodes[1];
        var deleteSourceNode = deleteStmt.ChildNodes[3];

        if (deleteTargetOpt.Term.Name == DeleteTargetOptTermName && deleteTargetOpt.ChildNodes.Count > 0)
        {
            sqlDeleteDefinition.Table = TableName.Create(deleteTargetOpt.ChildNodes[0]);
            sqlDeleteDefinition.SourceTable = TableName.Create(deleteSourceNode);
        }
        else
        {
            sqlDeleteDefinition.Table = TableName.Create(deleteSourceNode);
        }

        AddJoins(sqlDeleteDefinition, deleteStmt.ChildNodes[4]);
        sqlDeleteDefinition.WhereClause = WhereClauseOpt.Create(deleteStmt.ChildNodes[5]);
        sqlDeleteDefinition.Returning = ReturningClauseOpt.Create(deleteStmt.ChildNodes[6]);
    }

    protected virtual void AddJoins(SqlDeleteDefinition sqlDeleteDefinition, ParseTreeNode joinChainOptNode)
    {
        foreach (var join in JoinChainOpt.Create(joinChainOptNode))
        {
            sqlDeleteDefinition.Joins.Add(join);
        }
    }
}
