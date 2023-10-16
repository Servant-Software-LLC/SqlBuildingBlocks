using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class InsertStmt : NonTerminal
{
    private const string sVALUES = "VALUES";

    private readonly ExprList exprList;

    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// Don't forget to call SelectStmt.Initialize() and ExprList.Expr.Initialize()
    /// </summary>
    /// <param name="grammar"></param>
    public InsertStmt(Grammar grammar) : this(grammar, new Id(grammar)) { }
    public InsertStmt(Grammar grammar, Id id) : this(grammar, new SelectStmt(grammar, id)) { }
    public InsertStmt(Grammar grammar, SelectStmt selectStmt) : 
        this(grammar, selectStmt.Id, selectStmt.Expr, selectStmt) { }

    public InsertStmt(Grammar grammar, Id id, Expr expr, SelectStmt selectStmt)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Expr = expr ?? throw new ArgumentNullException(nameof(expr));
        SelectStmt = selectStmt ?? throw new ArgumentNullException(nameof(selectStmt));

        var INSERT = grammar.ToTerm("INSERT");
        var INTO = grammar.ToTerm("INTO");
        var VALUES = grammar.ToTerm(sVALUES);

        var intoOpt = new NonTerminal("intoOpt");
        intoOpt.Rule = grammar.Empty | INTO; //Into is optional in MSSQL

        IdList idList = new IdList(grammar, Id);

        var idlistPar = new NonTerminal("idlistPar");
        idlistPar.Rule = "(" + idList + ")";

        exprList = new(grammar, expr);

        var insertData = new NonTerminal("insertData");
        insertData.Rule = selectStmt | VALUES + "(" + exprList + ")";

        Rule = INSERT + intoOpt + id + idlistPar + insertData;

        grammar.MarkPunctuation("(", ")");
    }

    public Id Id { get; }
    public Expr Expr { get; }
    public SelectStmt SelectStmt { get; }


    public virtual SqlInsertDefinition Create(ParseTreeNode insertStmt)
    {
        SqlInsertDefinition sqlInsertDefinition = new();
        Update(insertStmt, sqlInsertDefinition);
        return sqlInsertDefinition;
    }

    public virtual SqlInsertDefinition Create(ParseTreeNode insertStmt, IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider,
                                              IFunctionProvider? functionProvider = null)
    {
        var sqlInsertDefinition = Create(insertStmt);

        if (sqlInsertDefinition.SelectDefinition != null)
            sqlInsertDefinition.SelectDefinition.ResolveReferences(databaseConnectionProvider, tableSchemaProvider, functionProvider);

        return sqlInsertDefinition;
    }

    public virtual void Update(ParseTreeNode insertStmt, SqlInsertDefinition sqlInsertDefinition)
    {
        if (insertStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {insertStmt.Term.Name} which does not match {TermName}", nameof(insertStmt));
        }

        sqlInsertDefinition.Table = Id.CreateTable(insertStmt.ChildNodes[2]);

        //Get the columns specified
        var idList = insertStmt.ChildNodes[3].ChildNodes[0];
        var columns = idList.ChildNodes.Create(Id.CreateColumn);

        foreach (SqlColumn column in columns)
        {
            if (string.IsNullOrEmpty(column.TableName))
                column.TableName = sqlInsertDefinition.Table.TableName;

            sqlInsertDefinition.Columns.Add(column);
        }

        //Determine if this is a VALUES clause or a SELECT statement
        var insertData = insertStmt.ChildNodes[4];
        bool isValues = insertData.ChildNodes[0].Term.Name == sVALUES;

        if (isValues)
        {
            sqlInsertDefinition.Values = exprList.Create(insertData.ChildNodes[1]);
        }
        else
        {
            sqlInsertDefinition.SelectDefinition = SelectStmt.Create(insertData.ChildNodes[0]);
        }

    }
}
