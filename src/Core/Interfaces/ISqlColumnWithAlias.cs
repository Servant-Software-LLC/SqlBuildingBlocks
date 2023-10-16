namespace SqlBuildingBlocks.Interfaces;

public interface ISqlColumnWithAlias : ISqlColumn
{
    string? ColumnAlias { get; set; }
}
