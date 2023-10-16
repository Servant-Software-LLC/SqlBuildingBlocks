using Irony.Parsing;
using SqlBuildingBlocks.Extensions;
using SqlBuildingBlocks.LogicalEntities;
using System.Reflection;

namespace SqlBuildingBlocks;

public class Id : NonTerminal
{
    public static string TermName => MethodBase.GetCurrentMethod().DeclaringType.Name.CamelCase();

    /// <summary>
    /// Helper ctor that assumes default <see cref="NonTerminal"/> types.  If you need different building blocks internally, use other ctor. 
    /// </summary>
    /// <param name="grammar"></param>
    public Id(Grammar grammar) : this(grammar, new SimpleId(grammar)) { }

    public Id(Grammar grammar, SimpleId id_simple)
        : base(nameof(Id).CamelCase())
    {
        SimpleId = id_simple ?? throw new ArgumentNullException(nameof(id_simple));

        var dot = grammar.ToTerm(".");

        Rule = grammar.MakePlusRule(this, dot, id_simple);
    }

    public SimpleId SimpleId { get; }

    public virtual SqlColumn CreateColumn(ParseTreeNode columnId)
    {
        if (columnId.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {columnId.Term.Name} which does not match {TermName}", nameof(columnId));
        }

        var columnBaseValues = GetColumnBaseValues(columnId);
        return new(columnBaseValues.DatabaseName, columnBaseValues.TableName, columnBaseValues.ColumnName);
    }

    public virtual SqlColumnRef CreateColumnRef(ParseTreeNode columnId)
    {
        if (columnId.Term.Name != TermName)
        {
            var thisMethod = MethodBase.GetCurrentMethod() as MethodInfo;
            throw new ArgumentException($"Cannot create building block of type {thisMethod!.ReturnType}.  The TermName for node is {columnId.Term.Name} which does not match {TermName}", nameof(columnId));
        }

        var columnBaseValues = GetColumnBaseValues(columnId);
        return new(columnBaseValues.DatabaseName, columnBaseValues.TableName, columnBaseValues.ColumnName);
    }


    /// <summary>
    /// Only retrieves the Id portion of the SqlTable, the alias is retrieved in the <see cref="TableName" class.  This keeps the id parsing and extraction from the <see cref="ParseTreeNode" logic all in one class./>/>
    /// </summary>
    /// <param name="tableId"></param>
    /// <returns></returns>
    public virtual SqlTable CreateTable(ParseTreeNode tableId)
    {
        string tableName;
        string? databaseName = null;

        switch (tableId.ChildNodes.Count)
        {
            case 1:
                tableName = tableId.ChildNodes[0].ChildNodes[0].Token.ValueString;
                break;
            case 2:
                databaseName = tableId.ChildNodes[0].ChildNodes[0].Token.ValueString;
                tableName = tableId.ChildNodes[1].ChildNodes[0].Token.ValueString;
                break;
            default:
                throw new ArgumentException($"{nameof(tableId)} has an unexpected number of child nodes.  Count = {tableId.ChildNodes[0].ChildNodes.Count}");
        }

        return new(databaseName, tableName);
    }

    internal (string? DatabaseName, string? TableName, string ColumnName) GetColumnBaseValues(ParseTreeNode columnId)
    {
        string? databaseName = null;
        string? tableName = null;
        string columnName;
        switch (columnId.ChildNodes.Count)
        {
            case 1:
                columnName = SimpleId.Create(columnId.ChildNodes[0]);
                break;
            case 2:
                tableName = SimpleId.Create(columnId.ChildNodes[0]);
                columnName = SimpleId.Create(columnId.ChildNodes[1]);
                break;
            case 3:
                databaseName = SimpleId.Create(columnId.ChildNodes[0]);
                tableName = SimpleId.Create(columnId.ChildNodes[1]);
                columnName = SimpleId.Create(columnId.ChildNodes[2]);
                break;
            default:
                throw new Exception($"Column {columnId} contained a name with zero or more than 3 parts.  Parts = {columnId.ChildNodes.Count}");
        }

        return new(databaseName, tableName, columnName);
    }
}
