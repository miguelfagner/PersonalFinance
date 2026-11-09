namespace PersonalFinance.Api.Data;

public sealed class SyncLog
{
    public long Id { get; set; }
    public string EntityType { get; set; } = "";
    public Guid ClientId { get; set; }
    public bool Deleted { get; set; }
    public string? Payload { get; set; }
    public Guid? ParentClientId { get; set; }
    public Guid? FixedClientId { get; set; }
}
public sealed class SyncReceipt
{
    public Guid Id { get; set; }
    public string Result { get; set; } = "";
}
