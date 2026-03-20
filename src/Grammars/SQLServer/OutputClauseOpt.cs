using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks.Grammars.SQLServer;

public class OutputClauseOpt : NonTerminal
{
    private const string OutputColumnTermName = "outputColumn";
    private const string OutputIntoOptTermName = "outputIntoOpt";

    private readonly Id id;

    public OutputClauseOpt(Grammar grammar, Id id)
        : base(TermName)
    {
        this.id = id;

        var OUTPUT = grammar.ToTerm("OUTPUT");
        var INTO = grammar.ToTerm("INTO");
        var INSERTED = grammar.ToTerm("INSERTED");
        var DELETED = grammar.ToTerm("DELETED");
        var DOT = grammar.ToTerm(".");
        var STAR = grammar.ToTerm("*");

        var outputSource = new NonTerminal("outputSource");
        outputSource.Rule = INSERTED | DELETED;

        var outputColumn = new NonTerminal(OutputColumnTermName);
        outputColumn.Rule = outputSource + DOT + id.SimpleId | outputSource + DOT + STAR;

        var outputColumnList = new NonTerminal("outputColumnList");
        outputColumnList.Rule = grammar.MakePlusRule(outputColumnList, grammar.ToTerm(","), outputColumn);

        var outputIntoOpt = new NonTerminal(OutputIntoOptTermName);
        outputIntoOpt.Rule = grammar.Empty | INTO + id;

        Rule = grammar.Empty
            | OUTPUT + outputColumnList + outputIntoOpt;

        grammar.MarkPunctuation(OUTPUT, DOT);
        grammar.MarkReservedWords("OUTPUT", "INSERTED", "DELETED");
    }

    public SqlOutputClause? Create(ParseTreeNode outputClauseOpt)
    {
        if (outputClauseOpt.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException(
                $"Cannot create building block of type {thisMethod!.ReturnType}. " +
                $"The TermName for node is {outputClauseOpt.Term.Name} which does not match {TermName}",
                nameof(outputClauseOpt));
        }

        // Empty rule matched — no OUTPUT clause
        if (outputClauseOpt.ChildNodes.Count == 0)
            return null;

        var result = new SqlOutputClause();

        // Child 0 is outputColumnList
        var outputColumnList = outputClauseOpt.ChildNodes[0];
        foreach (ParseTreeNode columnNode in outputColumnList.ChildNodes)
        {
            result.Columns.Add(CreateOutputColumn(columnNode));
        }

        // Child 1 is outputIntoOpt
        if (outputClauseOpt.ChildNodes.Count > 1)
        {
            var outputIntoOpt = outputClauseOpt.ChildNodes[1];
            if (outputIntoOpt.Term.Name == OutputIntoOptTermName && outputIntoOpt.ChildNodes.Count > 0)
            {
                // outputIntoOpt has: INTO + id
                // INTO is punctuated away if we mark it, but we didn't mark it as punctuation
                // ChildNodes[0] = INTO keyword, ChildNodes[1] = id
                // Actually, INTO is not punctuated, so child[0] = INTO, child[1] = id
                // Let's find the id node
                var intoTableNode = outputIntoOpt.ChildNodes[outputIntoOpt.ChildNodes.Count - 1];
                result.IntoTable = id.CreateTable(intoTableNode);
            }
        }

        return result;
    }

    private SqlOutputColumn CreateOutputColumn(ParseTreeNode columnNode)
    {
        // columnNode is outputColumn: outputSource + DOT + simpleId | outputSource + DOT + STAR
        // After punctuation (DOT is marked as punctuation):
        // Child 0 = outputSource (INSERTED or DELETED keyword)
        // Child 1 = simpleId or STAR

        var sourceNode = columnNode.ChildNodes[0];
        string source = sourceNode.ChildNodes.Count > 0
            ? sourceNode.ChildNodes[0].Token.ValueString.ToUpperInvariant()
            : sourceNode.Token.ValueString.ToUpperInvariant();

        var valueNode = columnNode.ChildNodes[1];
        if (valueNode.Token != null && valueNode.Token.ValueString == "*")
        {
            return new SqlOutputColumn(source, null);
        }
        else
        {
            string columnName = id.SimpleId.Create(valueNode);
            return new SqlOutputColumn(source, columnName);
        }
    }

    private static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();
}
