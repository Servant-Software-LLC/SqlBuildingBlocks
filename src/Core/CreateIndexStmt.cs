using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class CreateIndexStmt : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    public CreateIndexStmt(Grammar grammar, Id id)
        : this(grammar, id, new WhereClauseOpt(grammar)) { }

    public CreateIndexStmt(Grammar grammar, Id id, WhereClauseOpt whereClauseOpt)
        : base(TermName)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        WhereClauseOpt = whereClauseOpt ?? throw new ArgumentNullException(nameof(whereClauseOpt));

        var COMMA = grammar.ToTerm(",");
        var CREATE = grammar.ToTerm("CREATE");
        var UNIQUE = grammar.ToTerm("UNIQUE");
        var INDEX = grammar.ToTerm("INDEX");
        var ON = grammar.ToTerm("ON");
        var ASC = grammar.ToTerm("ASC");
        var DESC = grammar.ToTerm("DESC");

        var uniqueOpt = new NonTerminal("uniqueOpt");
        uniqueOpt.Rule = grammar.Empty | UNIQUE;

        var sortDirOpt = new NonTerminal("sortDirOpt");
        sortDirOpt.Rule = grammar.Empty | ASC | DESC;

        var indexColumn = new NonTerminal("indexColumn");
        indexColumn.Rule = id + sortDirOpt;

        var indexColumnList = new NonTerminal("indexColumnList");
        indexColumnList.Rule = grammar.MakePlusRule(indexColumnList, COMMA, indexColumn);

        Rule = CREATE + uniqueOpt + INDEX + id + ON + id + "(" + indexColumnList + ")" + whereClauseOpt;

        grammar.MarkPunctuation(CREATE, INDEX, ON);
        grammar.MarkPunctuation("(", ")");
    }

    public Id Id { get; }
    public WhereClauseOpt WhereClauseOpt { get; }

    public virtual SqlCreateIndexDefinition Create(ParseTreeNode createIndexStmt)
    {
        SqlCreateIndexDefinition definition = new();
        Update(createIndexStmt, definition);
        return definition;
    }

    public virtual void Update(ParseTreeNode createIndexStmt, SqlCreateIndexDefinition definition)
    {
        if (createIndexStmt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {createIndexStmt.Term.Name} which does not match {TermName}", nameof(createIndexStmt));
        }

        // After CREATE, INDEX, ON are removed as punctuation:
        //   ChildNodes[0] = uniqueOpt
        //   ChildNodes[1] = id (index name)
        //   ChildNodes[2] = id (table name)
        //   ChildNodes[3] = indexColumnList
        //   ChildNodes[4] = whereClauseOpt
        var uniqueNode = createIndexStmt.ChildNodes[0];
        definition.IsUnique = uniqueNode.ChildNodes.Count > 0;

        definition.IndexName = Id.CreateColumnRef(createIndexStmt.ChildNodes[1]).ColumnName;
        definition.Table = Id.CreateTable(createIndexStmt.ChildNodes[2]);

        var columnListNode = createIndexStmt.ChildNodes[3];
        foreach (ParseTreeNode colNode in columnListNode.ChildNodes)
        {
            // indexColumn: id + sortDirOpt
            var columnName = Id.CreateColumnRef(colNode.ChildNodes[0]).ColumnName;
            var sortDirNode = colNode.ChildNodes[1];
            bool descending = sortDirNode.ChildNodes.Count > 0 &&
                              string.Equals(sortDirNode.ChildNodes[0].Token?.Text, "DESC", StringComparison.OrdinalIgnoreCase);
            definition.Columns.Add(new SqlOrderByColumn(columnName, descending));
        }

        definition.WhereClause = WhereClauseOpt.Create(createIndexStmt.ChildNodes[4]);
    }
}
