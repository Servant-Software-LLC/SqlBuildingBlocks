using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class AlterStmt : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public AlterStmt(Grammar grammar, Id id)
        : this(grammar, id, new ColumnDef(grammar, id)) { }
    public AlterStmt(Grammar grammar, Id id, DataType dataType)
        : this(grammar, id, new ColumnDef(grammar, id, dataType)) { }
    public AlterStmt(Grammar grammar, Id id, Expr expr)
        : this(grammar, id, new ColumnDef(grammar, id, new DataType(grammar), expr), new ConstraintDef(grammar, id, expr)) { }
    public AlterStmt(Grammar grammar, Id id, DataType dataType, Expr expr)
        : this(grammar, id, new ColumnDef(grammar, id, dataType, expr), new ConstraintDef(grammar, id, expr)) { }

    public AlterStmt(Grammar grammar, Id id, ColumnDef columnDef)
        : this(grammar, id, columnDef, null) { }

    public AlterStmt(Grammar grammar, Id id, ColumnDef columnDef, ConstraintDef? constraintDef)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        ColumnDef = columnDef ?? throw new ArgumentNullException(nameof(columnDef));
        ConstraintDef = constraintDef;

        var ALTER = grammar.ToTerm("ALTER");
        var TABLE = grammar.ToTerm("TABLE");
        var ADD = grammar.ToTerm("ADD");
        var DROP = grammar.ToTerm("DROP");
        var COLUMN = grammar.ToTerm("COLUMN");
        var COLUMN_Optional = new NonTerminal("ColumnOptional", grammar.Empty | COLUMN);
        var alterCmd = new NonTerminal("alterCmd");

        if (constraintDef != null)
        {
            alterCmd.Rule = ADD + COLUMN_Optional + columnDef
                          | DROP + COLUMN_Optional + id
                          | ADD + constraintDef;
        }
        else
        {
            alterCmd.Rule = ADD + COLUMN_Optional + columnDef
                          | DROP + COLUMN_Optional + id;
        }

        Rule = ALTER + TABLE + id + alterCmd;

        grammar.MarkPunctuation("(", ")");

        // Mark ALTER, TABLE, and COLUMN as punctuation
        grammar.MarkPunctuation("ALTER", "TABLE", "COLUMN");
    }

    public Id Id { get; }
    public ColumnDef ColumnDef { get; }
    public ConstraintDef? ConstraintDef { get; }

    public virtual SqlAlterTableDefinition Create(ParseTreeNode alterStmt)
    {
        SqlAlterTableDefinition sqlAlterTableDefinition = new();
        Update(alterStmt, sqlAlterTableDefinition);

        return sqlAlterTableDefinition;
    }

    public virtual void Update(ParseTreeNode alterStmt, SqlAlterTableDefinition sqlAlterTableDefinition)
    {
        if (alterStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {alterStmt.Term.Name} which does not match {TermName}", nameof(alterStmt));
        }

        var id = Id.CreateTable(alterStmt.ChildNodes[0]);
        sqlAlterTableDefinition.Table = id;

        var alterCmd = alterStmt.ChildNodes[1];

        var alterType = alterCmd.ChildNodes[0].FindTokenAndGetText();

        if (alterType == "ADD")
        {
            // Check if this is ADD CONSTRAINT (2 children: ADD + constraintDef node)
            if (ConstraintDef != null && alterCmd.ChildNodes.Count == 2 &&
                alterCmd.ChildNodes[1].Term.Name == ConstraintDef.TermName)
            {
                var constraintResult = ConstraintDef.Create(alterCmd.ChildNodes[1], new List<SqlColumnDefinition>());
                if (constraintResult.Constraint != null)
                    sqlAlterTableDefinition.ConstraintsToAdd.Add(constraintResult.Constraint);
            }
            else
            {
                var constraints = new List<SqlConstraintDefinition>();
                var columnDef = ColumnDef.Create(alterCmd.ChildNodes[2], constraints);
                if (columnDef != null)
                    sqlAlterTableDefinition.ColumnsToAdd.Add(new(columnDef, constraints));
            }
        }
        else if (alterType == "DROP")
        {
            var columnName = alterCmd.ChildNodes[2].ChildNodes[0].ChildNodes[0].Token.ValueString;
            sqlAlterTableDefinition.ColumnsToDrop.Add(columnName);
        }
    }
}
