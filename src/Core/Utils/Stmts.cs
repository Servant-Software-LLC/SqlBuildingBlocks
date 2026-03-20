namespace SqlBuildingBlocks.Utils;

/// <summary>
/// A record instead of a class would have been more appropriate, but due to the library using .NET Standard 2.0,
/// this isn't readily available.
/// </summary>
internal class Stmts
{
    public Stmts(SelectStmt? selectStmt, InsertStmt? insertStmt, UpdateStmt? updateStmt, DeleteStmt? deleteStmt, CreateTableStmt? createTableStmt, AlterStmt? alterStmt, DropTableStmt? dropTableStmt = null, RenameTableStmt? renameTableStmt = null, MergeStmt? mergeStmt = null, CreateViewStmt? createViewStmt = null, DropViewStmt? dropViewStmt = null, AlterViewStmt? alterViewStmt = null, CreateIndexStmt? createIndexStmt = null, DropIndexStmt? dropIndexStmt = null, TransactionStmt? transactionStmt = null, SavepointStmt? savepointStmt = null)
    {
        SelectStmt = selectStmt;
        InsertStmt = insertStmt;
        UpdateStmt = updateStmt;
        DeleteStmt = deleteStmt;
        CreateTableStmt = createTableStmt;
        AlterStmt = alterStmt;
        DropTableStmt = dropTableStmt;
        RenameTableStmt = renameTableStmt;
        MergeStmt = mergeStmt;
        CreateViewStmt = createViewStmt;
        DropViewStmt = dropViewStmt;
        AlterViewStmt = alterViewStmt;
        CreateIndexStmt = createIndexStmt;
        DropIndexStmt = dropIndexStmt;
        TransactionStmt = transactionStmt;
        SavepointStmt = savepointStmt;
    }

    public SelectStmt? SelectStmt { get; }
    public InsertStmt? InsertStmt { get; }
    public UpdateStmt? UpdateStmt { get; }
    public DeleteStmt? DeleteStmt { get; }
    public CreateTableStmt? CreateTableStmt { get; }
    public AlterStmt? AlterStmt { get; }
    public DropTableStmt? DropTableStmt { get; }
    public RenameTableStmt? RenameTableStmt { get; }
    public MergeStmt? MergeStmt { get; }
    public CreateViewStmt? CreateViewStmt { get; }
    public DropViewStmt? DropViewStmt { get; }
    public AlterViewStmt? AlterViewStmt { get; }
    public CreateIndexStmt? CreateIndexStmt { get; }
    public DropIndexStmt? DropIndexStmt { get; }
    public TransactionStmt? TransactionStmt { get; }
    public SavepointStmt? SavepointStmt { get; }
}
