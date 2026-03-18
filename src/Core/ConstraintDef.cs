using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class ConstraintDef : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    private KeyTerm PRIMARY;
    private SimpleIdList simpleIdList;
    private IdList idList;

    public ConstraintDef(Grammar grammar, Id id)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));

        var CONSTRAINT = grammar.ToTerm("CONSTRAINT");
        PRIMARY = grammar.ToTerm("PRIMARY");
        var KEY = grammar.ToTerm("KEY");
        var UNIQUE = grammar.ToTerm("UNIQUE");
        var ON = grammar.ToTerm("ON");
        var DELETE = grammar.ToTerm("DELETE");
        var UPDATE = grammar.ToTerm("UPDATE");
        var CASCADE = grammar.ToTerm("CASCADE");
        var RESTRICT = grammar.ToTerm("RESTRICT");
        var NO = grammar.ToTerm("NO");
        var ACTION = grammar.ToTerm("ACTION");
        var SET = grammar.ToTerm("SET");
        var NULL = grammar.ToTerm("NULL");
        var DEFAULT = grammar.ToTerm("DEFAULT");

        var idlistPar = new NonTerminal("idlistPar");
        var constraintTypeOpt = new NonTerminal("constraintTypeOpt");

        simpleIdList = new SimpleIdList(id.SimpleId, grammar);
        var simpleIdListPar = new NonTerminal("simpleIdListPar");

        simpleIdListPar.Rule = "(" + simpleIdList + ")";

        idList = new IdList(grammar, id);
        idlistPar.Rule = "(" + idList + ")";

        var fkAction = new NonTerminal("fkAction");
        fkAction.Rule = CASCADE
                      | RESTRICT
                      | NO + ACTION
                      | SET + NULL
                      | SET + DEFAULT;

        var fkOnClause = new NonTerminal("fkOnClause");
        fkOnClause.Rule = ON + DELETE + fkAction
                        | ON + UPDATE + fkAction;

        var fkActionList = new NonTerminal("fkActionList");
        fkActionList.Rule = grammar.MakeStarRule(fkActionList, fkOnClause);

        constraintTypeOpt.Rule = PRIMARY + KEY + simpleIdListPar
                                | UNIQUE + simpleIdListPar
                                | "FOREIGN" + KEY + idlistPar + "REFERENCES" + id + idlistPar + fkActionList;

        Rule = CONSTRAINT + id.SimpleId + constraintTypeOpt;
    }

    public Id Id { get; }

    public (SqlConstraintDefinition? Constraint, bool Handled) Create(ParseTreeNode definition, IList<SqlColumnDefinition> columns)
    {
        if (definition.Term.Name != TermName)
            return (null, false);

        var constraintName = Id.SimpleId.Create(definition.ChildNodes[1]);
        var constraintTypeOpt = definition.ChildNodes[2];

        switch (constraintTypeOpt.ChildNodes.Count)
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

            // FOREIGN KEY: "FOREIGN" KEY idlistPar "REFERENCES" id idlistPar fkActionList
            case 7:
                return (new(constraintName, CreateForeignKeyConstraint(constraintTypeOpt)), true);

            default:
                throw new Exception("CREATE TABLE has an unrecognized CONSTRAINT on it.");
        }
    }

    private SqlForeignKeyConstraint CreateForeignKeyConstraint(ParseTreeNode constraintTypeOpt)
    {
        // constraintTypeOpt children: [0]=FOREIGN [1]=KEY [2]=idlistPar(local) [3]=REFERENCES [4]=id(table) [5]=idlistPar(ref) [6]=fkActionList
        var localColumnsNode = constraintTypeOpt.ChildNodes[2].ChildNodes[0]; // inside idlistPar: idList
        var referencedTableNode = constraintTypeOpt.ChildNodes[4];
        var referencedColumnsNode = constraintTypeOpt.ChildNodes[5].ChildNodes[0]; // inside idlistPar: idList
        var fkActionListNode = constraintTypeOpt.ChildNodes[6];

        var referencedTable = Id.CreateTable(referencedTableNode);
        var fkConstraint = new SqlForeignKeyConstraint(referencedTable);

        // Build column reference pairs (local column → referenced column)
        var localColumns = localColumnsNode.ChildNodes
            .Select(n => Id.SimpleId.Create(n.ChildNodes[0]))
            .ToList();
        var referencedColumns = referencedColumnsNode.ChildNodes
            .Select(n => Id.SimpleId.Create(n.ChildNodes[0]))
            .ToList();

        if (localColumns.Count != referencedColumns.Count)
            throw new Exception($"FOREIGN KEY constraint column count mismatch: {localColumns.Count} local columns but {referencedColumns.Count} referenced columns.");

        for (int i = 0; i < localColumns.Count; i++)
            fkConstraint.ColumnReferences.Add((localColumns[i], referencedColumns[i]));

        // Parse optional ON DELETE / ON UPDATE actions
        foreach (var onClause in fkActionListNode.ChildNodes)
        {
            // onClause children: [0]=ON [1]=DELETE|UPDATE [2]=fkAction
            var trigger = onClause.ChildNodes[1].Term.Name;
            var action = ParseFkAction(onClause.ChildNodes[2]);

            if (string.Equals(trigger, "DELETE", StringComparison.OrdinalIgnoreCase))
                fkConstraint.OnDeleteAction = action;
            else if (string.Equals(trigger, "UPDATE", StringComparison.OrdinalIgnoreCase))
                fkConstraint.OnUpdateAction = action;
        }

        return fkConstraint;
    }

    private static ForeignKeyReferentialAction ParseFkAction(ParseTreeNode fkActionNode)
    {
        var firstTerm = fkActionNode.ChildNodes[0].Term.Name;
        return firstTerm.ToUpperInvariant() switch
        {
            "CASCADE" => ForeignKeyReferentialAction.Cascade,
            "RESTRICT" => ForeignKeyReferentialAction.Restrict,
            "NO" => ForeignKeyReferentialAction.NoAction,
            "SET" => fkActionNode.ChildNodes[1].Term.Name.ToUpperInvariant() switch
            {
                "NULL" => ForeignKeyReferentialAction.SetNull,
                "DEFAULT" => ForeignKeyReferentialAction.SetDefault,
                _ => throw new Exception($"Unexpected SET action: {fkActionNode.ChildNodes[1].Term.Name}")
            },
            _ => throw new Exception($"Unexpected FK referential action: {firstTerm}")
        };
    }

    private static bool ContainsColumn(string column, IList<SqlColumnDefinition> columns) =>
        columns.Any(sqlColumn => sqlColumn.ColumnName == column);
}
