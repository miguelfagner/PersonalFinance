using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PersonalFinance.Api.Data;
using PersonalFinance.Contracts;

namespace PersonalFinance.Api.Services;

public sealed class SessionService(FinanceDbContext db, UserManager<IdentityUser> users, IConfiguration config)
{
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var allowedEmail = config["BootstrapUser:Email"];
        if (string.IsNullOrWhiteSpace(allowedEmail) || !string.Equals(request.Email, allowedEmail, StringComparison.OrdinalIgnoreCase)) return null;
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || !await users.CheckPasswordAsync(user, request.Password)) return null;
        return await IssueAsync(user, Guid.NewGuid(), ct);
    }

    public async Task<LoginResponse?> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return null;
        var hash = Hash(request.RefreshToken);
        var session = await db.RefreshSessions.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (session is null) return null;
        if (session.RevokedAtUtc is not null)
        {
            await RevokeFamilyAsync(session.FamilyId, ct);
            return null;
        }
        if (session.ExpiresAtUtc <= DateTime.UtcNow) return null;
        var user = await users.FindByIdAsync(session.UserId);
        if (user is null || !string.Equals(user.Email, config["BootstrapUser:Email"], StringComparison.OrdinalIgnoreCase)) return null;
        var changed = await db.RefreshSessions.Where(x => x.TokenHash == hash && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow), ct);
        if (changed != 1)
        {
            await RevokeFamilyAsync(session.FamilyId, ct);
            return null;
        }
        return await IssueAsync(user, session.FamilyId, ct);
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return;
        var hash = Hash(request.RefreshToken);
        await db.RefreshSessions.Where(x => x.TokenHash == hash && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow), ct);
    }

    public async Task<IdentityResult> ChangePasswordAsync(ChangePasswordRequest request, ClaimsPrincipal principal, CancellationToken ct)
    {
        var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId is null ? null : await users.FindByIdAsync(userId);
        if (user is null || !string.Equals(user.Email, config["BootstrapUser:Email"], StringComparison.OrdinalIgnoreCase))
            return IdentityResult.Failed(new IdentityError { Code = "InvalidAccount", Description = "Conta inválida." });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return result;

        await db.RefreshSessions.Where(x => x.UserId == user.Id && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow), ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, CancellationToken ct) =>
        db.RefreshSessions.Where(x => x.FamilyId == familyId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow), ct);

    private async Task<LoginResponse> IssueAsync(IdentityUser user, Guid familyId, CancellationToken ct)
    {
        var expires = DateTime.UtcNow.AddMinutes(15);
        var refreshExpires = DateTime.UtcNow.AddDays(30);
        var key = config["Authentication:JwtKey"] ?? throw new InvalidOperationException("Configure Authentication:JwtKey.");
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id), new Claim(JwtRegisteredClaimNames.Email, user.Email!) };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(config["Authentication:Issuer"], config["Authentication:Audience"], claims,
            expires: expires, signingCredentials: credentials);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        db.RefreshSessions.Add(new RefreshSession
        {
            TokenHash = Hash(refresh), UserId = user.Id, FamilyId = familyId, ExpiresAtUtc = refreshExpires
        });
        await db.SaveChangesAsync(ct);
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires)
        {
            RefreshToken = refresh, RefreshExpiresAt = refreshExpires
        };
    }
}
