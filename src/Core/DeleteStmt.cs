using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class DeleteStmt : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public DeleteStmt(Grammar grammar) : this(grammar, new Id(grammar)) { }
    public DeleteStmt(Grammar grammar, Id id) : this(grammar, new TableName(grammar, id), new WhereClauseOpt(grammar, id), new ReturningClauseOpt(grammar, id)) { }
    public DeleteStmt(Grammar grammar, SelectStmt selectStmt) : this(grammar, selectStmt.TableName, selectStmt.WhereClauseOpt, new ReturningClauseOpt(grammar, selectStmt.TableName.Id)) { }

    public DeleteStmt(Grammar sqlGrammar, TableName tableName, WhereClauseOpt whereClauseOpt, ReturningClauseOpt returningClauseOpt)
        : base(TermName)
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        WhereClauseOpt = whereClauseOpt ?? throw new ArgumentNullException(nameof(whereClauseOpt));
        ReturningClauseOpt = returningClauseOpt ?? throw new ArgumentNullException(nameof(returningClauseOpt));

        var DELETE = sqlGrammar.ToTerm("DELETE");
        var FROM = sqlGrammar.ToTerm("FROM");

        Rule = DELETE + FROM + tableName + whereClauseOpt + returningClauseOpt;
    }

    public TableName TableName { get; }
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

        sqlDeleteDefinition.Table = TableName.Create(deleteStmt.ChildNodes[2]);
        sqlDeleteDefinition.WhereClause = WhereClauseOpt.Create(deleteStmt.ChildNodes[3]);
        sqlDeleteDefinition.Returning = ReturningClauseOpt.Create(deleteStmt.ChildNodes[4]);
    }
}
