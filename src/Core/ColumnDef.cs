using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class ColumnDef : NonTerminal
{
    private const string DefaultOptName = "defaultOpt";
    private const string DefaultFuncCallName = "defaultFuncCall";
    private const string CheckOptName = "checkOpt";

    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    private Expr? expr;

    public ColumnDef(Grammar grammar, Id id)
        : this(grammar, id, new DataType(grammar))
    {

    }
    public ColumnDef(Grammar grammar, Id id, DataType dataType)
        : this(grammar, id, dataType, null)
    {
    }

    public ColumnDef(Grammar grammar, Id id, DataType dataType, Expr? expr)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
        LiteralValue = new LiteralValue(grammar);
        this.expr = expr;

        var NULL = grammar.ToTerm("NULL");
        var NOT = grammar.ToTerm("NOT");
        var UNIQUE = grammar.ToTerm("UNIQUE");
        var KEY = grammar.ToTerm("KEY");
        var PRIMARY = grammar.ToTerm("PRIMARY");
        var DEFAULT = grammar.ToTerm("DEFAULT");
        var CHECK = grammar.ToTerm("CHECK");

        var nullSpecOpt = new NonTerminal("nullSpecOpt");
        var uniqueOpt = new NonTerminal("uniqueOpt");
        var primaryKeyOpt = new NonTerminal("primaryKeyOpt");
        var defaultOpt = new NonTerminal(DefaultOptName);
        var defaultFuncCall = new NonTerminal(DefaultFuncCallName);
        var checkOpt = new NonTerminal(CheckOptName);

        //Inline constraints
        nullSpecOpt.Rule = NULL | NOT + NULL | grammar.Empty;
        uniqueOpt.Rule = UNIQUE | grammar.Empty;
        primaryKeyOpt.Rule = PRIMARY + KEY | grammar.Empty;

        // DEFAULT value: literal or no-arg function call (e.g. GETDATE(), NOW())
        defaultFuncCall.Rule = id.SimpleId + "(" + ")";
        defaultOpt.Rule = DEFAULT + LiteralValue | DEFAULT + defaultFuncCall | grammar.Empty;
        grammar.MarkPunctuation(DEFAULT);

        if (expr != null)
        {
            var checkExpr = new NonTerminal("columnCheckExpr");
            checkExpr.Rule = "(" + expr + ")";
            checkOpt.Rule = CHECK + checkExpr | grammar.Empty;
            Rule = id.SimpleId + DataType + defaultOpt + nullSpecOpt + uniqueOpt + primaryKeyOpt + checkOpt;
        }
        else
        {
            checkOpt.Rule = grammar.Empty;
            Rule = id.SimpleId + DataType + defaultOpt + nullSpecOpt + uniqueOpt + primaryKeyOpt;
        }
    }

    public Id Id { get; }

    public DataType DataType { get; }

    public LiteralValue LiteralValue { get; }

    public SqlColumnDefinition? Create(ParseTreeNode definition, IList<SqlConstraintDefinition> constraintDefinitions)
    {
        if (definition.Term.Name != TermName)
            return null;

        var columnName = Id.SimpleId.Create(definition.ChildNodes[0]);
        var dataTypeName = DataType.Create(definition.ChildNodes[1]);

        SqlColumnDefinition sqlColumnDefinition = new(columnName, dataTypeName);

        //DEFAULT value
        var defaultOpt = definition.ChildNodes[2];
        if (defaultOpt.ChildNodes.Count > 0)
        {
            var defaultChild = defaultOpt.ChildNodes[0];
            if (defaultChild.Term.Name == DefaultFuncCallName)
            {
                var funcName = Id.SimpleId.Create(defaultChild.ChildNodes[0]);
                sqlColumnDefinition.DefaultFunctionValue = new SqlFunction(funcName);
            }
            else
            {
                sqlColumnDefinition.DefaultLiteralValue = LiteralValue.Create(defaultChild);
            }
        }

        //NULL or NOT NULL
        var nullSpecOpt = definition.ChildNodes[3];
        if (nullSpecOpt.ChildNodes.Count > 0)
        {
            sqlColumnDefinition.AllowNulls = nullSpecOpt.ChildNodes.Count == 1;
        }

        //UNIQUE
        var uniqueOpt = definition.ChildNodes[4];
        if (uniqueOpt.ChildNodes.Count > 0)
        {
            var uniqueConstraint = new SqlUniqueConstraint();
            uniqueConstraint.Columns.Add(columnName);
            constraintDefinitions.Add(new($"UQ_{columnName}", uniqueConstraint));
        }

        //PRIMARY KEY
        var primaryKeyOpt = definition.ChildNodes[5];
        if (primaryKeyOpt.ChildNodes.Count > 0)
        {
            var primaryKeyConstraint = new SqlPrimaryKeyConstraint();
            primaryKeyConstraint.Columns.Add(columnName);
            constraintDefinitions.Add(new($"PK_{columnName}", primaryKeyConstraint));
        }

        // Inline CHECK constraint (only present when expr was provided)
        if (expr != null && definition.ChildNodes.Count > 6)
        {
            var checkOpt = definition.ChildNodes[6];
            if (checkOpt.ChildNodes.Count > 0)
            {
                // checkOpt: CHECK + columnCheckExpr; columnCheckExpr has 1 child (expr) after parens removed
                var checkExprNode = checkOpt.ChildNodes[1]; // columnCheckExpr node
                var exprNode = checkExprNode.ChildNodes[0];
                var checkExpression = expr.Create(exprNode);
                constraintDefinitions.Add(new($"CK_{columnName}", new SqlCheckConstraint(checkExpression)));
            }
        }

        return sqlColumnDefinition;
    }

}
