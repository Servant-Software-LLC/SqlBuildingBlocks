namespace SqlBuildingBlocks.Utils;

/// <summary>
/// A record instead of a class would have been more appropriate, but due to the library using .NET Standard 2.0,
/// this isn't readily available.
/// </summary>
internal class Stmts
{
    public Stmts(SelectStmt? selectStmt, InsertStmt? insertStmt, UpdateStmt? updateStmt, DeleteStmt? deleteStmt, CreateTableStmt? createTableStmt)
    {
        SelectStmt = selectStmt;
        InsertStmt = insertStmt;
        UpdateStmt = updateStmt;
        DeleteStmt = deleteStmt;
        CreateTableStmt = createTableStmt;
    }

    public SelectStmt? SelectStmt { get; }
    public InsertStmt? InsertStmt { get; }
    public UpdateStmt? UpdateStmt { get; }
    public DeleteStmt? DeleteStmt { get; }
    public CreateTableStmt? CreateTableStmt { get; }
}