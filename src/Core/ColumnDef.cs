using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class ColumnDef : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public ColumnDef(Grammar grammar, Id id)
        : this(grammar, id, new DataType(grammar))
    {
        
    }
    public ColumnDef(Grammar grammar, Id id, DataType dataType)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));

        var NULL = grammar.ToTerm("NULL");
        var NOT = grammar.ToTerm("NOT");
        var UNIQUE = grammar.ToTerm("UNIQUE");
        var KEY = grammar.ToTerm("KEY");
        var PRIMARY = grammar.ToTerm("PRIMARY");

        var nullSpecOpt = new NonTerminal("nullSpecOpt");
        var uniqueOpt = new NonTerminal("uniqueOpt");
        var primaryKeyOpt = new NonTerminal("primaryKeyOpt");

        //Inline constraints
        nullSpecOpt.Rule = NULL | NOT + NULL | grammar.Empty;
        uniqueOpt.Rule = UNIQUE | grammar.Empty;
        primaryKeyOpt.Rule = PRIMARY + KEY | grammar.Empty;

        Rule = id.SimpleId + DataType + nullSpecOpt + uniqueOpt + primaryKeyOpt;
    }

    public Id Id { get; }

    public DataType DataType { get; }

    public SqlColumnDefinition? Create(ParseTreeNode definition, IList<SqlConstraintDefinition> constraintDefinitions)
    {
        if (definition.Term.Name != TermName)
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

}
