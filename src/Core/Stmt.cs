using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities;
using SqlBuildingBlocks.Utils;
using System.Reflection;

namespace SqlBuildingBlocks;

public class Stmt : NonTerminal
{
    private readonly Stmts stmts;

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// Note:  There is no need to call InitializeRule methods on <see cref="Expr"/>, because it is done 
    ///        for you by this ctor.
    /// </summary>
    /// <param name="grammar"></param>
    public Stmt(Grammar grammar) : this(grammar, new Id(grammar)) { }
    public Stmt(Grammar grammar, Id id) : this(grammar, id, new SelectStmt(grammar, id)) { }
    public Stmt(Grammar grammar, Id id, SelectStmt selectStmt) : 
        this(grammar, selectStmt, new FuncCall(grammar, selectStmt.JoinChainOpt.Expr.Id, selectStmt.JoinChainOpt.Expr), new WhereClauseOpt(grammar, selectStmt.JoinChainOpt.Expr)) { }

    public Stmt(Grammar grammar, SelectStmt selectStmt, FuncCall funcCall, WhereClauseOpt whereClauseOpt)
        : base(TermName)
    {
        selectStmt.Expr.InitializeRule(selectStmt, funcCall);

        InsertStmt insertStmt = new(grammar, selectStmt);
        UpdateStmt updateStmt = new(grammar, selectStmt.TableName, funcCall, whereClauseOpt);
        DeleteStmt deleteStmt = new(grammar, selectStmt.TableName, whereClauseOpt, updateStmt.ReturningClauseOpt);
        CreateTableStmt createTableStmt = new(grammar, selectStmt.Id);
        AlterStmt alterStmt = new(grammar, selectStmt.Id, createTableStmt.ColumnDef);

        var internalState = DetermineInternalState(selectStmt, insertStmt, updateStmt, deleteStmt, createTableStmt, alterStmt);
        stmts = internalState.Stmts;
        Rule = internalState.Rule;
        grammar.MarkTransient(this);
    }

    public Stmt(Grammar grammar, params NonTerminal[] stmts)
        : base(TermName)
    {
        var internalState = DetermineInternalState(stmts);
        this.stmts = internalState.Stmts;
        Rule = internalState.Rule;
        grammar.MarkTransient(this);
    }

    private static (BnfExpression Rule, Stmts Stmts) DetermineInternalState(params NonTerminal[] stmts)
    {
        if (stmts == null || stmts.Length == 0)
            throw new ArgumentException($"You must provide at least one statment in the ctor of {nameof(Stmt)}.");

        if (stmts[0] == null)
            throw new ArgumentNullException($"{nameof(stmts)}[0]");

        BnfExpression? rule = null;
        SelectStmt? selectStmt = null;
        InsertStmt? insertStmt = null;
        UpdateStmt? updateStmt = null;
        DeleteStmt? deleteStmt = null;
        CreateTableStmt? createTableStmt = null;
        AlterStmt? alterStmt = null;

        //Compose the rule for this instance from all of the provided statments.
        for (int i = 0; i < stmts.Length; i++)
        {
            var stmt = stmts[i];

            if (stmt == null)
                throw new ArgumentNullException($"{nameof(stmts)}[{i}]");

            switch (stmt)
            {
                case SelectStmt selectStmt1:
                    selectStmt = selectStmt1;
                    break;
                case InsertStmt insertStmt1:
                    insertStmt = insertStmt1;
                    break;
                case UpdateStmt updateStmt1:
                    updateStmt = updateStmt1;
                    break;
                case DeleteStmt deleteStmt1:
                    deleteStmt = deleteStmt1;
                    break;
                case CreateTableStmt createTableStmt1:
                    createTableStmt = createTableStmt1;
                    break;
                case AlterStmt alterStmt1:
                    alterStmt = alterStmt1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"The statement of type {stmt.GetType()} was not expected.");
            }

            if (i == 0)
                rule = stmt;
            else
                rule |= stmts[i];
        }

        return new(rule!, new(selectStmt, insertStmt, updateStmt, deleteStmt, createTableStmt, alterStmt));
    }

    public virtual SqlDefinition Create(ParseTreeNode stmt)
    {
        //SELECT
        if (stmt.Term.Name == SelectStmt.TermName)
        {
            if (stmts.SelectStmt == null)
                throw new ArgumentNullException(nameof(stmts.SelectStmt), $"Unable to create a {nameof(SqlDefinition)} instance for Irony term, {stmt.Term.Name}, because a {typeof(SelectStmt)} was not provided to the ctor of {nameof(Stmt)}");

            return new(stmts.SelectStmt.Create(stmt));
        }

        //INSERT
        if (stmt.Term.Name == InsertStmt.TermName)
        {
            if (stmts.InsertStmt == null)
                throw new ArgumentNullException(nameof(stmts.InsertStmt), $"Unable to create a {nameof(SqlDefinition)} instance for Irony term, {stmt.Term.Name}, because a {typeof(InsertStmt)} was not provided to the ctor of {nameof(Stmt)}");

            return new(stmts.InsertStmt.Create(stmt));
        }

        //UPDATE
        if (stmt.Term.Name == UpdateStmt.TermName)
        {
            if (stmts.UpdateStmt == null)
                throw new ArgumentNullException(nameof(stmts.UpdateStmt), $"Unable to create a {nameof(SqlDefinition)} instance for Irony term, {stmt.Term.Name}, because a {typeof(UpdateStmt)} was not provided to the ctor of {nameof(Stmt)}");

            return new(stmts.UpdateStmt.Create(stmt));
        }

        //DELETE
        if (stmt.Term.Name == DeleteStmt.TermName)
        {
            if (stmts.DeleteStmt == null)
                throw new ArgumentNullException(nameof(stmts.DeleteStmt), $"Unable to create a {nameof(SqlDefinition)} instance for Irony term, {stmt.Term.Name}, because a {typeof(DeleteStmt)} was not provided to the ctor of {nameof(Stmt)}");

            return new(stmts.DeleteStmt.Create(stmt));
        }

        //CREATE TABLE
        if (stmt.Term.Name == CreateTableStmt.TermName)
        {
            if (stmts.CreateTableStmt == null)
                throw new ArgumentNullException(nameof(stmts.CreateTableStmt), $"Unable to create a {nameof(SqlDefinition)} instance for Irony term, {stmt.Term.Name}, because a {typeof(CreateTableStmt)} was not provided to the ctor of {nameof(Stmt)}");

            return new(stmts.CreateTableStmt.Create(stmt));
        }

        //ALTER TABLE
        if (stmt.Term.Name == AlterStmt.TermName)
        {
            if (stmts.AlterStmt == null)
                throw new ArgumentNullException(nameof(stmts.AlterStmt), $"Unable to create a {nameof(SqlDefinition)} instance for Irony term, {stmt.Term.Name}, because a {typeof(AlterStmt)} was not provided to the ctor of {nameof(Stmt)}");

            return new(stmts.AlterStmt.Create(stmt));
        }

        var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
        throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {stmt.Term.Name} which does not match any of the SQL statement types ({SelectStmt.TermName}, {InsertStmt.TermName}, {UpdateStmt.TermName}, {DeleteStmt.TermName}) or {CreateTableStmt.TermName}", nameof(stmt));
    }

    public virtual SqlDefinition Create(ParseTreeNode stmt, IDatabaseConnectionProvider databaseConnectionProvider, ITableSchemaProvider tableSchemaProvider,
                                        IFunctionProvider? functionProvider = null)
    {
        var sqlDefinition = Create(stmt);

        if (sqlDefinition.Select != null)
        {
            sqlDefinition.Select.ResolveReferences(databaseConnectionProvider, tableSchemaProvider, functionProvider);
        }
        else if (sqlDefinition.Insert?.SelectDefinition != null)
        {
            sqlDefinition.Insert?.SelectDefinition.ResolveReferences(databaseConnectionProvider, tableSchemaProvider, functionProvider);
        }

        return sqlDefinition;
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
