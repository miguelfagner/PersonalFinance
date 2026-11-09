using SQLite;
namespace PersonalFinance.Resources.Models;
public sealed class SyncPending
{
    [PrimaryKey, AutoIncrement] public long Id { get; set; }
    public Guid OperationId { get; set; }
    public string EntityType { get; set; } = "";
    public Guid ClientId { get; set; }
    public bool Deleted { get; set; }
    public string? Payload { get; set; }
    public long? BaseRevision { get; set; }
}
public sealed class SyncControl
{
    [PrimaryKey] public int Id { get; set; } = 1;
    public bool Suppressed { get; set; }
    public long Cursor { get; set; }
    public bool Initialized { get; set; }
    public bool NeedsReconciliation { get; set; }
    public DateTime? LastSyncUtc { get; set; }
}
public sealed class SyncDeleted
{
    [PrimaryKey] public string Key { get; set; } = "";
}
public sealed class SyncEntityRevision
{
    [PrimaryKey] public string Key { get; set; } = "";
    public long Revision { get; set; }
}
public sealed class SyncRejected
{
    [PrimaryKey] public Guid OperationId { get; set; }
    public string EntityType { get; set; } = "";
    public Guid ClientId { get; set; }
    public string Payload { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime RejectedAtUtc { get; set; }
}
public sealed class SyncCommandPending
{
    [PrimaryKey] public Guid OperationId { get; set; }
    public string Server { get; set; } = "";
    public string Email { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
