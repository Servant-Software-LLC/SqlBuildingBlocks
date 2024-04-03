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

    public AlterStmt(Grammar grammar, Id id, ColumnDef columnDef)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        ColumnDef = columnDef ?? throw new ArgumentNullException(nameof(columnDef));

        var ALTER = grammar.ToTerm("ALTER");
        var TABLE = grammar.ToTerm("TABLE");
        var ADD = grammar.ToTerm("ADD");
        var DROP = grammar.ToTerm("DROP");
        var COLUMN = grammar.ToTerm("COLUMN");
        var COLUMN_Optional = new NonTerminal("ColumnOptional", grammar.Empty | COLUMN);
        var alterCmd = new NonTerminal("alterCmd");

        alterCmd.Rule = ADD + COLUMN_Optional + columnDef
                      | DROP + COLUMN_Optional + id;

        Rule = ALTER + TABLE + id + alterCmd;

        grammar.MarkPunctuation("(", ")");

        // Mark ALTER, TABLE, and COLUMN as punctuation
        grammar.MarkPunctuation("ALTER", "TABLE", "COLUMN");
    }

    public Id Id { get; }
    public ColumnDef ColumnDef { get; }

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
            var constraints = new List<SqlConstraintDefinition>();
            var columnDef = ColumnDef.Create(alterCmd.ChildNodes[2], constraints);
            if (columnDef != null)
                sqlAlterTableDefinition.ColumnsToAdd.Add(new(columnDef, constraints));
        }
        else if (alterType == "DROP")
        {
            var columnName = alterCmd.ChildNodes[2].ChildNodes[0].ChildNodes[0].Token.ValueString;
            sqlAlterTableDefinition.ColumnsToDrop.Add(columnName);
        }
    }
}
