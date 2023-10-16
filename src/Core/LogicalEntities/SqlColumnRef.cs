using SqlBuildingBlocks.Interfaces;
using SqlBuildingBlocks.LogicalEntities.BaseClasses;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlColumnRef : SqlColumnBase
{
    /// <summary>
    /// This could be referencing either a <see cref="SqlColumn"/> or a <see cref="SqlFunction"/> with an alias. (Later an aggregate, when/if implemented)
    /// </summary>
    public ISqlColumn? Column { get; set; }

    public SqlColumnRef(string? databaseName, string? tableName, string columnName)
        : base(databaseName, tableName, columnName)
    {
    }

    public bool RefersTo(SqlColumn column)
    {
        //If DatabaseName, TableName and ColumnName are match or if just TableName and ColumnName match.
        if ((string.IsNullOrEmpty(DatabaseName) || string.Compare(DatabaseName, column.DatabaseName, true) == 0) &&
                string.Compare(TableName, column.TableName, true) == 0 && string.Compare(ColumnName, column.ColumnName, true) == 0)
            return true;

        //If no TableName, then just check on ColumnName or ColumnAlias matching.
        if (string.IsNullOrEmpty(TableName) &&
            (string.Compare(ColumnName, column.ColumnName, true) == 0 || string.Compare(ColumnName, column.ColumnAlias, true) == 0))
            return true;

        return false;
    }

    public SqlExpression? Accept(ISqlExpressionVisitor visitor) => visitor.Visit(this);

    public Type Type 
    { 
        get
        {
            switch (Column)
            {
                case SqlColumn column:
                    return column.ColumnType ?? typeof(string);

                default:
                    throw new InvalidOperationException($"Unable to get {nameof(Type)} of {this}");
            }
        }
    }

    public string ToExpressionString() 
    {
        var expressionString = ColumnName;
        if (!string.IsNullOrEmpty(TableName))
        {
            expressionString = $"{TableName}.{expressionString}";

            if (!string.IsNullOrEmpty(DatabaseName))
                expressionString = $"{DatabaseName}.{expressionString}";
        }

        return expressionString;
    }

    public override string ToString()
    {
        if (Column == null)
            return $"{ToExpressionString()}<null {nameof(SqlColumnRef)}>";

        switch (Column)
        {
            case SqlFunctionColumn functionColumn:
                return functionColumn.ToString();
            case SqlColumn column:
                return column.ToString();

            default:
                return $"<unknown {nameof(SqlColumnRef)}>";
        }
    }
}
