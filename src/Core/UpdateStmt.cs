using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class UpdateStmt : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public UpdateStmt(Grammar grammar) : this(grammar, new Id(grammar)) { }
    public UpdateStmt(Grammar grammar, Id id) :
        this(grammar, new TableName(grammar, id), new FuncCall(grammar, id), new WhereClauseOpt(grammar, id))  { }
    public UpdateStmt(Grammar grammar, SelectStmt selectStmt) : this(grammar, selectStmt.TableName, selectStmt.FuncCall, selectStmt.WhereClauseOpt) { }
    public UpdateStmt(Grammar grammar, TableName tableName, FuncCall funcCall, WhereClauseOpt whereClauseOpt) :
        this(grammar, whereClauseOpt.Expr.Id, whereClauseOpt.Expr.LiteralValue, whereClauseOpt.Expr.Parameter, funcCall, tableName, whereClauseOpt, new(grammar, whereClauseOpt.Expr.Id))   { }
    public UpdateStmt(Grammar grammar, Id id, LiteralValue literalValue, Parameter parameter, FuncCall funcCall, TableName tableName, WhereClauseOpt whereClauseOpt) :
        this(grammar, id, literalValue, parameter, funcCall, tableName, whereClauseOpt, new(grammar, id)) { }


    public UpdateStmt(Grammar grammar, Id id, LiteralValue literalValue, Parameter parameter, 
                      FuncCall funcCall, TableName tableName, WhereClauseOpt whereClauseOpt,
                      ReturningClauseOpt returningClauseOpt)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        LiteralValue = literalValue ?? throw new ArgumentNullException(nameof(literalValue));
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        FuncCall = funcCall ?? throw new ArgumentNullException(nameof(funcCall));
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        WhereClauseOpt = whereClauseOpt ?? throw new ArgumentNullException(nameof(whereClauseOpt));
        ReturningClauseOpt = returningClauseOpt ?? throw new ArgumentNullException(nameof(returningClauseOpt));

        var UPDATE = grammar.ToTerm("UPDATE");
        var SET = grammar.ToTerm("SET");
        var COMMA = grammar.ToTerm(",");

        // TODO: To simplify matters (for now), we only will take a literal value.
        // Context for future efforts:  The right side of a SET clause in an UPDATE statement is used to provide a new value for a field. So, it can contain any expression that results in a value of a type
        // compatible with the column being updated. This includes literal values, column references, function calls, arithmetic expressions, subqueries that return a single value, etc.
        //
        // Examples to later consider:
        //   UPDATE my_table SET column1 = column2 + column3
        //   UPDATE my_table SET column1 = column1 + 10
        //   UPDATE my_table SET column1 = (SELECT column2 FROM other_table WHERE id = 1) WHERE id = 1;
        //   UPDATE my_table SET column1 = CASE WHEN column2 > 10 THEN 'High' ELSE 'Low' END;
        //
        var assignmentValue = new NonTerminal("assignmentValue");
        assignmentValue.Rule = literalValue | parameter | funcCall;

        var assignment = new NonTerminal("assignment");
        assignment.Rule = id + "=" + assignmentValue;

        var assignList = new NonTerminal("assignList");
        assignList.Rule = grammar.MakePlusRule(assignList, COMMA, assignment);


        Rule = UPDATE + tableName + SET + assignList + whereClauseOpt + returningClauseOpt;

        grammar.MarkTransient(assignmentValue);
    }

    public Id Id { get; }
    public LiteralValue LiteralValue { get; }
    public Parameter Parameter { get; }
    public FuncCall FuncCall { get; }
    public TableName TableName { get; }
    public WhereClauseOpt WhereClauseOpt { get; }
    public ReturningClauseOpt ReturningClauseOpt { get; }

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

        var assignList = updateStmt.ChildNodes[3];
        foreach (ParseTreeNode assignment in assignList.ChildNodes)
        {
            var sqlColumn = Id.CreateColumn(assignment.ChildNodes[0]);
            SqlAssignment sqlAssignment = CreateAssignment(sqlColumn, assignment.ChildNodes[2]);

            sqlUpdateDefinition.Assignments.Add(sqlAssignment);
        }

        sqlUpdateDefinition.WhereClause = WhereClauseOpt.Create(updateStmt.ChildNodes[4]);

        var returningClause = updateStmt.ChildNodes[5];
        sqlUpdateDefinition.Returning = ReturningClauseOpt.Create(returningClause);
    }

    protected virtual SqlAssignment CreateAssignment(SqlColumn sqlColumn, ParseTreeNode assignmentValue)
    {
        if (assignmentValue.Term.Name == LiteralValue.TermName)
            return new(sqlColumn, LiteralValue.Create(assignmentValue));

        if (assignmentValue.Term.Name == Parameter.TermName)
            return new(sqlColumn, Parameter.Create(assignmentValue));

        if (assignmentValue.Term.Name == FuncCall.TermName)
            return new(sqlColumn, FuncCall.Create(assignmentValue));

        throw new ArgumentException($"Unable to {nameof(CreateAssignment)} because {nameof(assignmentValue)} does not have a recognized Term.Name.  Term.Name = {assignmentValue.Term.Name}");
    }
}
