namespace PersonalFinance.Api.Data;

public sealed class RefreshSession
{
    public string TokenHash { get; set; } = "";
    public string UserId { get; set; } = "";
    public Guid FamilyId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
