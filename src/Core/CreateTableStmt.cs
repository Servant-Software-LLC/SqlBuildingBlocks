using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class CreateTableStmt : NonTerminal
{
    protected const string ColumnDefName = "columnDef";

    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public CreateTableStmt(Grammar grammar) : this(grammar, new Id(grammar)) { }
    public CreateTableStmt(Grammar grammar, Id id) : this(grammar, id, new ColumnDef(grammar, id), new ConstraintDef(grammar, id)) { }
    public CreateTableStmt(Grammar grammar, Id id, DataType dataType) : this(grammar, id, new ColumnDef(grammar, id, dataType), new ConstraintDef(grammar, id)) { }
    public CreateTableStmt(Grammar grammar, Id id, ColumnDef columnDef, ConstraintDef constraintDef)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        ColumnDef = columnDef ?? throw new ArgumentNullException(nameof(columnDef));
        ConstraintDef = constraintDef ?? throw new ArgumentNullException(nameof(constraintDef));

        //TODO:  PreferShiftHere() in Grammar is protected.  Solicit the owners to get it made public.  We would like to avoid having to make our
        //       own derived "Grammar" class whose sole purpose is just to expose it.  Adds lots of burden on our consumers.
        //
        //PreferredActionHint preferShiftHere = new(PreferredActionType.Shift);

        var COMMA = grammar.ToTerm(",");
        var CREATE = grammar.ToTerm("CREATE");
        var TABLE = grammar.ToTerm("TABLE");

        var columnOrConstraintDef = new NonTerminal("columnOrConstraintDef");
        var columnAndConstraintList = new NonTerminal("columnDefAndConstraintList");
        
        columnOrConstraintDef.Rule = constraintDef | columnDef;
        columnAndConstraintList.Rule = grammar.MakePlusRule(columnAndConstraintList, COMMA, columnOrConstraintDef);

        Rule = CREATE + TABLE + id + "(" + columnAndConstraintList + ")";
        grammar.MarkTransient(columnOrConstraintDef);
        grammar.MarkPunctuation(CREATE, TABLE);
        grammar.MarkPunctuation("(", ")");
    }

    public Id Id { get; }
    public ColumnDef ColumnDef { get; }
    public ConstraintDef ConstraintDef { get; }


    public virtual SqlCreateTableDefinition Create(ParseTreeNode createTableStmt)
    {
        SqlCreateTableDefinition sqlCreateTableDefinition = new();
        Update(createTableStmt, sqlCreateTableDefinition);

        return sqlCreateTableDefinition;
    }

    public virtual void Update(ParseTreeNode createTableStmt, SqlCreateTableDefinition sqlCreateTableDefinition)
    {
        if (createTableStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {createTableStmt.Term.Name} which does not match {TermName}", nameof(createTableStmt));
        }

        sqlCreateTableDefinition.Table = Id.CreateTable(createTableStmt.ChildNodes[0]);

        var columnsAndConstraintDefinitions = createTableStmt.ChildNodes[1];
        foreach (ParseTreeNode definition in columnsAndConstraintDefinitions.ChildNodes)
        {
            var columnDefinition = ColumnDef.Create(definition, sqlCreateTableDefinition.Constraints);
            if (columnDefinition != null)
            {
                sqlCreateTableDefinition.Columns.Add(columnDefinition);
                continue;
            }

            var constraintResult = ConstraintDef.Create(definition, sqlCreateTableDefinition.Columns);
            if (constraintResult.Constraint != null)
            {
                sqlCreateTableDefinition.Constraints.Add(constraintResult.Constraint);
                continue;
            }

            if (!constraintResult.Handled)
                throw new Exception($"The definition {definition} wasn't either a column or constraint definition");
        }
    }

}
