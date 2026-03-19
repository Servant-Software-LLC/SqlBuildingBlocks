using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class AlterStmt : NonTerminal
{
    private const string AlterColumnDefTermName = "alterColumnDef";
    private const string AlterColumnDefaultDefTermName = "alterColumnDefaultDef";
    private const string AlterColumnDefaultFuncCallTermName = "alterColumnDefaultFuncCall";

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
        var CONSTRAINT = grammar.ToTerm("CONSTRAINT");
        var RENAME = grammar.ToTerm("RENAME");
        var MODIFY = grammar.ToTerm("MODIFY");
        var CHANGE = grammar.ToTerm("CHANGE");
        var COLUMN = grammar.ToTerm("COLUMN");
        var TO = grammar.ToTerm("TO");
        var TYPE = grammar.ToTerm("TYPE");
        var SET = grammar.ToTerm("SET");
        var NULL = grammar.ToTerm("NULL");
        var NOT = grammar.ToTerm("NOT");
        var DEFAULT = grammar.ToTerm("DEFAULT");
        var COLUMN_Optional = new NonTerminal("ColumnOptional", grammar.Empty | COLUMN);
        var alterCmd = new NonTerminal("alterCmd");
        var alterColumnDef = new NonTerminal(AlterColumnDefTermName);
        var alterColumnDefaultDef = new NonTerminal(AlterColumnDefaultDefTermName);
        var alterColumnDefaultFuncCall = new NonTerminal(AlterColumnDefaultFuncCallTermName);
        var nullSpecOpt = new NonTerminal("alterColumnNullSpecOpt");

        nullSpecOpt.Rule = NULL | NOT + NULL | grammar.Empty;
        alterColumnDef.Rule = TYPE + ColumnDef.DataType
                            | ColumnDef.DataType + nullSpecOpt;
        alterColumnDefaultFuncCall.Rule = id.SimpleId + "(" + ")";
        alterColumnDefaultDef.Rule = SET + DEFAULT + ColumnDef.LiteralValue
                                   | SET + DEFAULT + alterColumnDefaultFuncCall
                                   | DROP + DEFAULT;

        if (constraintDef != null)
        {
            alterCmd.Rule = ADD + COLUMN_Optional + columnDef
                          | DROP + COLUMN_Optional + id
                          | DROP + CONSTRAINT + id
                          | ADD + constraintDef
                          | RENAME + COLUMN_Optional + id + TO + id
                          | MODIFY + COLUMN_Optional + columnDef
                          | CHANGE + COLUMN_Optional + id + columnDef
                          | ALTER + COLUMN_Optional + id + alterColumnDef
                          | ALTER + COLUMN_Optional + id + alterColumnDefaultDef;
        }
        else
        {
            alterCmd.Rule = ADD + COLUMN_Optional + columnDef
                          | DROP + COLUMN_Optional + id
                          | DROP + CONSTRAINT + id
                          | RENAME + COLUMN_Optional + id + TO + id
                          | MODIFY + COLUMN_Optional + columnDef
                          | CHANGE + COLUMN_Optional + id + columnDef
                          | ALTER + COLUMN_Optional + id + alterColumnDef
                          | ALTER + COLUMN_Optional + id + alterColumnDefaultDef;
        }

        Rule = ALTER + TABLE + id + alterCmd;

        grammar.MarkPunctuation("(", ")");

        // Mark ALTER, TABLE, COLUMN, and TO as punctuation
        grammar.MarkPunctuation("ALTER", "TABLE", "COLUMN", "TO");
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

        var alterColumnDefNode = alterCmd.ChildNodes.FirstOrDefault(node => node.Term.Name == AlterColumnDefTermName);
        if (alterColumnDefNode != null)
        {
            var columnIdNode = alterCmd.ChildNodes.First(node => node.Term.Name == Id.TermName);
            var columnName = GetSimpleId(columnIdNode);
            var columnDef = CreateAlterColumnDefinition(alterColumnDefNode, columnName);
            sqlAlterTableDefinition.ColumnsToAlter.Add(new(columnName, columnDef, SqlAlterColumnOperation.Alter));
            return;
        }

        var alterColumnDefaultDefNode = alterCmd.ChildNodes.FirstOrDefault(node => node.Term.Name == AlterColumnDefaultDefTermName);
        if (alterColumnDefaultDefNode != null)
        {
            var columnIdNode = alterCmd.ChildNodes.First(node => node.Term.Name == Id.TermName);
            var columnName = GetSimpleId(columnIdNode);
            sqlAlterTableDefinition.ColumnDefaultsToAlter.Add(CreateAlterColumnDefaultAction(alterColumnDefaultDefNode, columnName));
            return;
        }

        var alterType = alterCmd.ChildNodes
            .Select(node => node.FindTokenAndGetText())
            .FirstOrDefault(text => !string.IsNullOrEmpty(text));

        var idNodes = alterCmd.ChildNodes.Where(node => node.Term.Name == Id.TermName).ToList();
        var columnDefNode = alterCmd.ChildNodes.FirstOrDefault(node => node.Term.Name == ColumnDef.TermName);

        if (alterType == "ADD")
        {
            // Check if this is ADD CONSTRAINT (2 children: ADD + constraintDef node)
            if (ConstraintDef != null && alterCmd.ChildNodes.Any(node => node.Term.Name == ConstraintDef.TermName))
            {
                var constraintNode = alterCmd.ChildNodes.First(node => node.Term.Name == ConstraintDef.TermName);
                var constraintResult = ConstraintDef.Create(constraintNode, new List<SqlColumnDefinition>());
                if (constraintResult.Constraint != null)
                    sqlAlterTableDefinition.ConstraintsToAdd.Add(constraintResult.Constraint);
            }
            else if (columnDefNode != null)
            {
                var constraints = new List<SqlConstraintDefinition>();
                var columnDef = ColumnDef.Create(columnDefNode, constraints);
                if (columnDef != null)
                    sqlAlterTableDefinition.ColumnsToAdd.Add(new(columnDef, constraints));
            }
        }
        else if (alterType == "DROP")
        {
            var columnName = GetSimpleId(idNodes.Single());
            if (alterCmd.ChildNodes.Any(node => node.Term.Name == "CONSTRAINT"))
                sqlAlterTableDefinition.ConstraintsToDrop.Add(columnName);
            else
                sqlAlterTableDefinition.ColumnsToDrop.Add(columnName);
        }
        else if (alterType == "RENAME")
        {
            var oldName = GetSimpleId(idNodes[0]);
            var newName = GetSimpleId(idNodes[1]);
            sqlAlterTableDefinition.ColumnsToRename.Add((oldName, newName));
        }
        else if (alterType == "MODIFY" && columnDefNode != null)
        {
            var constraints = new List<SqlConstraintDefinition>();
            var columnDef = ColumnDef.Create(columnDefNode, constraints);
            if (columnDef != null)
                sqlAlterTableDefinition.ColumnsToAlter.Add(new(columnDef.ColumnName, columnDef, SqlAlterColumnOperation.Modify));
        }
        else if (alterType == "CHANGE" && columnDefNode != null)
        {
            var oldName = GetSimpleId(idNodes.Single());
            var constraints = new List<SqlConstraintDefinition>();
            var columnDef = ColumnDef.Create(columnDefNode, constraints);
            if (columnDef != null)
                sqlAlterTableDefinition.ColumnsToAlter.Add(new(oldName, columnDef, SqlAlterColumnOperation.Change));
        }
    }

    private string GetSimpleId(ParseTreeNode idNode) => Id.SimpleId.Create(idNode.ChildNodes[0]);

    private SqlColumnDefinition CreateAlterColumnDefinition(ParseTreeNode alterColumnDef, string columnName)
    {
        var dataTypeNode = alterColumnDef.ChildNodes[0].Term.Name == "TYPE"
            ? alterColumnDef.ChildNodes[1]
            : alterColumnDef.ChildNodes[0];

        var columnDefinition = new SqlColumnDefinition(columnName, ColumnDef.DataType.Create(dataTypeNode));

        if (alterColumnDef.ChildNodes[0].Term.Name != "TYPE" && alterColumnDef.ChildNodes.Count > 1)
        {
            var nullSpecOpt = alterColumnDef.ChildNodes[1];
            if (nullSpecOpt.ChildNodes.Count > 0)
                columnDefinition.AllowNulls = nullSpecOpt.ChildNodes.Count == 1;
        }

        return columnDefinition;
    }

    private SqlAlterColumnDefaultAction CreateAlterColumnDefaultAction(ParseTreeNode alterColumnDefaultDef, string columnName)
    {
        var operation = alterColumnDefaultDef.ChildNodes[0].Term.Name;
        if (operation == "DROP")
            return new(columnName, SqlAlterColumnDefaultOperation.DropDefault);

        var defaultValueNode = alterColumnDefaultDef.ChildNodes[alterColumnDefaultDef.ChildNodes.Count - 1];
        if (defaultValueNode.Term.Name == AlterColumnDefaultFuncCallTermName)
        {
            var funcName = Id.SimpleId.Create(defaultValueNode.ChildNodes[0]);
            return new(columnName, SqlAlterColumnDefaultOperation.SetDefault, defaultFunctionValue: new SqlFunction(funcName));
        }

        return new(columnName, SqlAlterColumnDefaultOperation.SetDefault, defaultLiteralValue: ColumnDef.LiteralValue.Create(defaultValueNode));
    }
}
