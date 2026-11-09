using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PersonalFinance.Api.Data;
using PersonalFinance.Api.Services;
using PersonalFinance.Contracts;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Finance") ?? throw new InvalidOperationException("Configure ConnectionStrings:Finance.");
var jwtKey = builder.Configuration["Authentication:JwtKey"] ?? throw new InvalidOperationException("Configure Authentication:JwtKey.");
if (jwtKey.Length < 32) throw new InvalidOperationException("Authentication:JwtKey deve ter ao menos 32 caracteres.");
builder.Services.AddDbContext<FinanceDbContext>(o => o.UseSqlServer(connection));
builder.Services.AddIdentityCore<IdentityUser>(o => { o.Password.RequiredLength = 12; o.User.RequireUniqueEmail = true; }).AddEntityFrameworkStores<FinanceDbContext>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new()
{
    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
    ValidIssuer = builder.Configuration["Authentication:Issuer"], ValidAudience = builder.Configuration["Authentication:Audience"],
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ClockSkew = TimeSpan.FromMinutes(1)
});
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? []).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<CommandService>();
builder.Services.AddScoped<SqliteImporter>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<DeviceSnapshotService>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddScoped<SessionService>();
var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (Exception error) when (!context.Response.HasStarted && error is ArgumentException or KeyNotFoundException or InvalidOperationException or DbUpdateException)
    {
        var status = error is KeyNotFoundException ? 404 : error is InvalidOperationException or DbUpdateException ? 409 : 400;
        await Results.Problem(statusCode: status, detail: error is DbUpdateException ? "O lançamento mudou ou possui vínculos inválidos. Atualize a tela e tente novamente." : error.Message).ExecuteAsync(context);
    }
});
app.UseHttpsRedirection();
if (app.Environment.IsDevelopment()) app.UseWebAssemblyDebugging();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseCors(); app.UseAuthentication(); app.UseAuthorization();
await BootstrapUserAsync(app.Services, app.Configuration);

app.MapPost("/api/auth/login", async (LoginRequest request, SessionService sessions, CancellationToken ct) =>
    await sessions.LoginAsync(request, ct) is { } response ? Results.Ok(response) : Results.Unauthorized()).AllowAnonymous();
app.MapPost("/api/auth/refresh", async (RefreshRequest request, SessionService sessions, CancellationToken ct) =>
    await sessions.RefreshAsync(request, ct) is { } response ? Results.Ok(response) : Results.Unauthorized()).AllowAnonymous();
app.MapPost("/api/auth/logout", async (RefreshRequest request, SessionService sessions, CancellationToken ct) =>
{
    await sessions.LogoutAsync(request, ct);
    return Results.NoContent();
}).AllowAnonymous();
app.MapPost("/api/auth/change-password", async (ChangePasswordRequest request, SessionService sessions, ClaimsPrincipal principal, CancellationToken ct) =>
{
    var result = await sessions.ChangePasswordAsync(request, principal, ct);
    return result.Succeeded ? Results.NoContent() : Results.ValidationProblem(
        result.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(e => e.Description).ToArray()));
}).RequireAuthorization();

var api = app.MapGroup("/api").RequireAuthorization();
api.MapFinance();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();

static async Task BootstrapUserAsync(IServiceProvider services, IConfiguration config)
{
    using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>(); await db.Database.MigrateAsync();
    // Existing installations need a complete first feed without reimporting their database.
    if (!await db.SyncLogs.AnyAsync())
    {
        foreach (var row in await db.FixedIncomes.ToListAsync()) db.Entry(row).State = EntityState.Modified;
        foreach (var row in await db.FixedExpenses.ToListAsync()) db.Entry(row).State = EntityState.Modified;
        foreach (var row in await db.Incomes.ToListAsync()) db.Entry(row).State = EntityState.Modified;
        foreach (var row in await db.Expenses.ToListAsync()) db.Entry(row).State = EntityState.Modified;
        foreach (var row in await db.Payments.ToListAsync()) db.Entry(row).State = EntityState.Modified;
        foreach (var row in await db.MonthlyPreparationItems.ToListAsync()) db.Entry(row).State = EntityState.Modified;
        await db.SaveChangesAsync();
    }
    var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(); if (await users.Users.AnyAsync()) return;
    var email = config["BootstrapUser:Email"]; var password = config["BootstrapUser:Password"];
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Defina BootstrapUser:Email e BootstrapUser:Password antes da primeira execução.");
    var result = await users.CreateAsync(new IdentityUser { UserName = email, Email = email, EmailConfirmed = true }, password);
    if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
}
