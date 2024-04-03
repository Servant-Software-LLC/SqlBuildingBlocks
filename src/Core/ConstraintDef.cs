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

    public ConstraintDef(Grammar grammar, Id id)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));

        var CONSTRAINT = grammar.ToTerm("CONSTRAINT");
        PRIMARY = grammar.ToTerm("PRIMARY");
        var KEY = grammar.ToTerm("KEY");
        var UNIQUE = grammar.ToTerm("UNIQUE");

        var idlistPar = new NonTerminal("idlistPar");
        var constraintTypeOpt = new NonTerminal("constraintTypeOpt");

        simpleIdList = new SimpleIdList(id.SimpleId, grammar);
        var simpleIdListPar = new NonTerminal("simpleIdListPar");

        simpleIdListPar.Rule = "(" + simpleIdList + ")";

        IdList idList = new(grammar, id);
        idlistPar.Rule = "(" + idList + ")";
        constraintTypeOpt.Rule = PRIMARY + KEY + simpleIdListPar
                                | UNIQUE + simpleIdListPar
                                | "FOREIGN" + KEY + idlistPar + "REFERENCES" + id + idlistPar;


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
