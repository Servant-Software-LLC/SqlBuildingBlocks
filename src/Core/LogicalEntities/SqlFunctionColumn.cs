using SqlBuildingBlocks.Interfaces;

namespace SqlBuildingBlocks.LogicalEntities;

public class SqlFunctionColumn : ISqlColumn, ISqlColumnWithAlias
{
    public SqlFunctionColumn(SqlFunction function)
    {
        Function = function ?? throw new ArgumentNullException(nameof(function));
    }

    public SqlFunction Function { get; }
    public string? ColumnAlias { get; set; }

    public Type ColumnType => Function.ValueType ?? throw new ArgumentNullException(nameof(Function));

    public string? ColumnName => Function.FunctionName;

    public override string ToString() => ColumnAlias == null ? Function.ToString() : $"{Function} AS {ColumnAlias}";
}
