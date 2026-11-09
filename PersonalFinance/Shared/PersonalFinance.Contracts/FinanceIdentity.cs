using System.Security.Cryptography;
using System.Text;

namespace PersonalFinance.Contracts;

public static class FinanceIdentity
{
    // Both applications identify the same model/month snapshot, even when generated offline.
    public static Guid ForMonth(string kind, Guid model, DateOnly month) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"PersonalFinance|{kind}|{model:D}|{month:yyyy-MM}"))[..16]);
    public static Guid ForSource(string kind, Guid source) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"PersonalFinance|{kind}|{source:D}"))[..16]);
}
