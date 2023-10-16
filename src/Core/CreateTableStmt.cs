using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class CreateTableStmt : NonTerminal
{
    protected const string ColumnDefName = "columnDef";
    protected const string ConstraintDef = "constraintDef";

    private KeyTerm PRIMARY;
    private SimpleIdList simpleIdList;

    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public CreateTableStmt(Grammar grammar) : this(grammar, new Id(grammar)) { }
    public CreateTableStmt(Grammar grammar, Id id) : this(grammar, id, new DataType(grammar)) { }
    public CreateTableStmt(Grammar grammar, Id id, DataType dataType)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));

        //TODO:  PreferShiftHere() in Grammar is protected.  Solicit the owners to get it made public.  We would like to avoid having to make our
        //       own derived "Grammar" class whose sole purpose is just to expose it.  Adds lots of burden on our consumers.
        //
        PreferredActionHint preferShiftHere = new(PreferredActionType.Shift);

        var COMMA = grammar.ToTerm(",");
        var NULL = grammar.ToTerm("NULL");
        var NOT = grammar.ToTerm("NOT");
        var CREATE = grammar.ToTerm("CREATE");
        var TABLE = grammar.ToTerm("TABLE");
        var CONSTRAINT = grammar.ToTerm("CONSTRAINT");
        var KEY = grammar.ToTerm("KEY");
        PRIMARY = grammar.ToTerm("PRIMARY");
        var UNIQUE = grammar.ToTerm("UNIQUE");

        var columnOrConstraintDef = new NonTerminal("columnOrConstraintDef");
        var columnAndConstraintList = new NonTerminal("columnDefAndConstraintList");
        var constraintDef = new NonTerminal(ConstraintDef);
        var constraintTypeOpt = new NonTerminal("constraintTypeOpt");
        var simpleIdPar = new NonTerminal("simpleIdPar");
        var idlistPar = new NonTerminal("idlistPar");
        simpleIdList = new SimpleIdList(id.SimpleId, grammar);
        var simpleIdListPar = new NonTerminal("simpleIdListPar");

        simpleIdPar.Rule = "(" + id.SimpleId + ")";
        IdList idList = new(grammar, id);
        idlistPar.Rule = "(" + idList + ")";
        simpleIdListPar.Rule = "(" + simpleIdList + ")";
        constraintTypeOpt.Rule = PRIMARY + KEY + simpleIdListPar
                                | UNIQUE + simpleIdListPar
                                | NOT + NULL + simpleIdPar
                                | "FOREIGN" + KEY + idlistPar + "REFERENCES" + id + idlistPar;
        constraintDef.Rule = CONSTRAINT + id.SimpleId + constraintTypeOpt;

        var columnDef = ComposeColumnDef(grammar);
        columnOrConstraintDef.Rule = constraintDef | columnDef;
        columnAndConstraintList.Rule = grammar.MakePlusRule(columnAndConstraintList, COMMA, columnOrConstraintDef);

        Rule = CREATE + TABLE + id + "(" + columnAndConstraintList + ")";
        grammar.MarkTransient(columnOrConstraintDef, simpleIdPar);
        grammar.MarkPunctuation(CREATE, TABLE);
        grammar.MarkPunctuation("(", ")");
    }

    public Id Id { get; }
    public DataType DataType { get; }


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
            var columnDefinition = CreateColumnDefinition(definition, sqlCreateTableDefinition.Constraints);
            if (columnDefinition != null)
            {
                sqlCreateTableDefinition.Columns.Add(columnDefinition);
                continue;
            }

            var constraintResult = CreateConstraintDefinition(definition, sqlCreateTableDefinition.Columns);
            if (constraintResult.Constraint != null)
            {
                sqlCreateTableDefinition.Constraints.Add(constraintResult.Constraint);
                continue;
            }

            if (!constraintResult.Handled)
                throw new Exception($"The definition {definition} wasn't either a column or constraint definition");
        }
    }

    protected virtual NonTerminal ComposeColumnDef(Grammar grammar)
    {
        var NULL = grammar.ToTerm("NULL");
        var NOT = grammar.ToTerm("NOT");
        var UNIQUE = grammar.ToTerm("UNIQUE");
        var KEY = grammar.ToTerm("KEY");

        var nullSpecOpt = new NonTerminal("nullSpecOpt");
        var uniqueOpt = new NonTerminal("uniqueOpt");
        var primaryKeyOpt = new NonTerminal("primaryKeyOpt");

        //Inline constraints
        nullSpecOpt.Rule = NULL | NOT + NULL | grammar.Empty;
        uniqueOpt.Rule = UNIQUE | grammar.Empty;
        primaryKeyOpt.Rule = PRIMARY + KEY | grammar.Empty;

        var columnDef = new NonTerminal(ColumnDefName);
        columnDef.Rule = Id.SimpleId + DataType + nullSpecOpt + uniqueOpt + primaryKeyOpt;

        return columnDef;
    }

    private SqlColumnDefinition? CreateColumnDefinition(ParseTreeNode definition, IList<SqlConstraintDefinition> constraintDefinitions)
    {
        if (definition.Term.Name != ColumnDefName)
            return null;

        var columnName = Id.SimpleId.Create(definition.ChildNodes[0]);
        var dataTypeName = DataType.Create(definition.ChildNodes[1]);

        SqlColumnDefinition sqlColumnDefinition = new(columnName, dataTypeName);

        //NULL or NOT NULL
        var nullSpecOpt = definition.ChildNodes[2];
        if (nullSpecOpt.ChildNodes.Count > 0)
        {
            sqlColumnDefinition.AllowNulls = nullSpecOpt.ChildNodes.Count == 1;
        }

        //UNIQUE
        var uniqueOpt = definition.ChildNodes[3];
        if (uniqueOpt.ChildNodes.Count > 0)
        {
            var uniqueConstraint = new SqlUniqueConstraint();
            uniqueConstraint.Columns.Add(columnName);
            constraintDefinitions.Add(new($"UQ_{columnName}", uniqueConstraint));
        }

        //PRIMARY KEY
        var primaryKeyOpt = definition.ChildNodes[4];
        if (primaryKeyOpt.ChildNodes.Count > 0)
        {
            var primaryKeyConstraint = new SqlPrimaryKeyConstraint();
            primaryKeyConstraint.Columns.Add(columnName);
            constraintDefinitions.Add(new($"PK_{columnName}", primaryKeyConstraint));
        }    

        return sqlColumnDefinition;
    }

    private (SqlConstraintDefinition? Constraint, bool Handled) CreateConstraintDefinition(ParseTreeNode definition, IList<SqlColumnDefinition> columns)
    {
        if (definition.Term.Name != ConstraintDef)
            return (null, false);

        var constraintName = Id.SimpleId.Create(definition.ChildNodes[1]);
        var constraintTypeOpt = definition.ChildNodes[2];

        switch(constraintTypeOpt.ChildNodes.Count)
        {
            //Only UNIQUE has 2 child nodes
            case 2:
                SqlUniqueConstraint sqlUniqueConstraint = new();
                var uniqueColumns = simpleIdList.Create(constraintTypeOpt.ChildNodes[1].ChildNodes[0]);
                foreach (var column in uniqueColumns)
                {
                    if (!ContainsColumn(column, columns))
                        throw new Exception($"CREATE TABLE statement does not contain a column definition for {column}, but there is a UNIQUE constraint specifying this column.");

                    sqlUniqueConstraint.Columns.Add(column);
                }
                return (new(constraintName, sqlUniqueConstraint), true);

            //Could be either PRIMARY KEY or NOT NULL constraints
            case 3:
                //PRIMARY KEY
                if (constraintTypeOpt.ChildNodes[0].Term.Name == PRIMARY.Name)
                {
                    SqlPrimaryKeyConstraint sqlPrimaryKeyConstraint = new();
                    var primaryKeyColumns = simpleIdList.Create(constraintTypeOpt.ChildNodes[2].ChildNodes[0]);
                    foreach (var column in primaryKeyColumns)
                    {
                        var columnFound = columns.FirstOrDefault(col => col.ColumnName == column);
                        if (columnFound == null)
                            throw new Exception($"CREATE TABLE statement does not contain a column definition for {column}, but there is a PRIMARY KEY constraint specifying this column.");

                        //PRIMARY KEY constraints implicitly make a column NOT NULL
                        columnFound.AllowNulls = false;

                        sqlPrimaryKeyConstraint.Columns.Add(column);
                    }
                    return (new(constraintName, sqlPrimaryKeyConstraint), true);
                }

                //NOT NULL
                else
                {
                    var notNullColumnName = Id.SimpleId.Create(constraintTypeOpt.ChildNodes[2].ChildNodes[0]);
                    var column = columns.FirstOrDefault(col => col.ColumnName == notNullColumnName);
                    if (column == null)
                        throw new Exception($"CREATE TABLE statement does not contain a column definition for {notNullColumnName}, but there is a NOT NULL constraint specifying this column.");

                    column.AllowNulls = false;
                    return (null, true);
                }

            case 6:
                throw new NotImplementedException($"CREATE TABLE has a FOREIGN KEY constraint, but no implementation has been made for the {nameof(CreateTableStmt)}.{nameof(Create)} method.  Consider contributing an implementation.");

            default:
                throw new Exception("CREATE TABLE has an unrecognized CONSTRAINT on it.");
        }

        throw new NotImplementedException();
    }

    private static bool ContainsColumn(string column, IList<SqlColumnDefinition> columns) =>
        columns.Any(sqlColumn => sqlColumn.ColumnName == column);

}
