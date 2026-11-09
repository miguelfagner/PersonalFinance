using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PersonalFinance.Api.Data;
using PersonalFinance.Contracts;

namespace PersonalFinance.Api.Services;

public static class FinanceEndpoints
{
    public static void MapFinance(this RouteGroupBuilder api)
    {
api.MapPost("/commands", async (FinanceCommand command, CommandService service, CancellationToken ct) =>
{
    var result = await service.ExecuteAsync(command, ct);
    return result.Status == "Conflict" ? Results.Conflict(result) : Results.Ok(result);
});
api.MapGet("/dashboard/{year:int}/{month:int}", async (int year, int month, DateOnly? today, FinanceService service, CancellationToken ct) => Results.Ok(await service.BalanceAsync(new DateOnly(year, month, 1), ct, today)));
api.MapGet("/expenses/{year:int}/{month:int}", async (int year, int month, FinanceService service, CancellationToken ct) => Results.Ok(await service.ExpensesAsync(new DateOnly(year, month, 1), ct)));
api.MapGet("/incomes/{year:int}/{month:int}", async (int year, int month, FinanceDbContext db, CancellationToken ct) => Results.Ok(await db.Incomes.Where(x => x.ReferenceMonth.Year == year && x.ReferenceMonth.Month == month).OrderBy(x => x.Payer).ToListAsync(ct)));
api.MapGet("/lookups/incomes", async (string? q, FinanceDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Incomes.AsNoTracking().Where(x => string.IsNullOrEmpty(q) || x.Payer.Contains(q) || x.Description.Contains(q))
        .OrderByDescending(x => x.ReferenceMonth).ThenBy(x => x.Payer).Take(100).ToListAsync(ct)));
api.MapGet("/lookups/expenses", async (string? q, FinanceDbContext db, CancellationToken ct) =>
{
    var rows = await db.Expenses.AsNoTracking().Include(x => x.Payments)
        .Where(x => string.IsNullOrEmpty(q) || x.Description.Contains(q) || x.Category.Contains(q))
        .OrderByDescending(x => x.DueDate).ThenBy(x => x.Description).Take(100).ToListAsync(ct);
    return Results.Ok(rows.Select(x => new ExpenseListItem(x, x.Payments.Sum(p => p.Amount),
        Math.Max(0, x.Amount - x.Payments.Sum(p => p.Amount)), "")));
});
api.MapGet("/fixed-incomes", async (FinanceDbContext db, CancellationToken ct) => Results.Ok(await db.FixedIncomes.OrderBy(x => x.Payer).ToListAsync(ct)));
api.MapGet("/fixed-expenses", async (FinanceDbContext db, CancellationToken ct) => Results.Ok(await db.FixedExpenses.OrderBy(x => x.DueDay).ToListAsync(ct)));
api.MapGet("/prepare/{year:int}/{month:int}", async (int year, int month, FinanceService service, CancellationToken ct) => Results.Ok(await service.PreviewAsync(new DateOnly(year, month, 1), ct)));
api.MapPost("/prepare", async (PrepareMonthRequest request, FinanceService service, IConfiguration config, CancellationToken ct) =>
    Compatibility(config) ? Results.Ok(await service.PrepareAsync(request,ct)) : LegacyWrite());
api.MapPost("/incomes", (Income item, FinanceService service, IConfiguration config, CancellationToken ct) => LegacySaveAsync(item,service,config,ct));
api.MapPut("/incomes/{id:int}", (int id, Income item, FinanceService service, IConfiguration config, CancellationToken ct) => { item.Id=id;return LegacySaveAsync(item,service,config,ct); });
api.MapPost("/expenses", (Expense item, FinanceService service, IConfiguration config, CancellationToken ct) => LegacySaveAsync(item,service,config,ct));
api.MapPut("/expenses/{id:int}", (int id, Expense item, FinanceService service, IConfiguration config, CancellationToken ct) => { item.Id=id;return LegacySaveAsync(item,service,config,ct); });
api.MapPost("/payments", (Payment item, FinanceService service, IConfiguration config, CancellationToken ct) => LegacySaveAsync(item,service,config,ct));
api.MapPut("/payments/{id:int}", (int id, Payment item, FinanceService service, IConfiguration config, CancellationToken ct) => { item.Id=id;return LegacySaveAsync(item,service,config,ct); });
api.MapPost("/fixed-incomes", (FixedIncome item, FinanceService service, IConfiguration config, CancellationToken ct) => LegacySaveAsync(item,service,config,ct));
api.MapPut("/fixed-incomes/{id:int}", (int id, FixedIncome item, FinanceService service, IConfiguration config, CancellationToken ct) => { item.Id=id;return LegacySaveAsync(item,service,config,ct); });
api.MapPost("/fixed-expenses", (FixedExpense item, FinanceService service, IConfiguration config, CancellationToken ct) => LegacySaveAsync(item,service,config,ct));
api.MapPut("/fixed-expenses/{id:int}", (int id, FixedExpense item, FinanceService service, IConfiguration config, CancellationToken ct) => { item.Id=id;return LegacySaveAsync(item,service,config,ct); });
api.MapDelete("/incomes/{id:int}", (int id, FinanceService service, IConfiguration config, CancellationToken ct) => LegacyDeleteAsync<Income>(id,service,config,ct));
api.MapDelete("/expenses/{id:int}", (int id, FinanceService service, IConfiguration config, CancellationToken ct) => LegacyDeleteAsync<Expense>(id,service,config,ct));
api.MapDelete("/payments/{id:int}", (int id, FinanceService service, IConfiguration config, CancellationToken ct) => LegacyDeleteAsync<Payment>(id,service,config,ct));
api.MapDelete("/fixed-incomes/{id:int}", (int id, FinanceService service, IConfiguration config, CancellationToken ct) => LegacyDeleteAsync<FixedIncome>(id,service,config,ct));
api.MapDelete("/fixed-expenses/{id:int}", (int id, FinanceService service, IConfiguration config, CancellationToken ct) => LegacyDeleteAsync<FixedExpense>(id,service,config,ct));
api.MapGet("/incomes/{id:int}", async (int id, FinanceDbContext db, CancellationToken ct) => await db.Incomes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) is Income x ? Results.Ok(x) : Results.NotFound());
api.MapGet("/expenses/{id:int}", async (int id, FinanceDbContext db, CancellationToken ct) => await db.Expenses.AsNoTracking().Include(x => x.Payments).SingleOrDefaultAsync(x => x.Id == id, ct) is Expense x ? Results.Ok(x) : Results.NotFound());
api.MapGet("/payments/{id:int}", async (int id, FinanceDbContext db, CancellationToken ct) => await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) is Payment x ? Results.Ok(x) : Results.NotFound());
api.MapGet("/payments", (int year, int month, int? expenseId, string? category, string? categories, int? week, bool? personalDueMonth, FinanceService service, CancellationToken ct) => service.PaymentsAsync(new(year, month, 1), expenseId, category, week, ct, categories?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), personalDueMonth == true));
api.MapGet("/open/{year:int}/{month:int}", async (int year, int month, FinanceService service, CancellationToken ct) => (await service.ExpensesAsync(new(year, month, 1), ct)).Where(x => x.Remaining > 0));
api.MapPost("/expenses/{id:int}/settle", async (int id, SettleRequest request, FinanceService service, IConfiguration config, CancellationToken ct) =>
    Compatibility(config) ? Results.Ok(await service.SettleAsync(id,request,ct)) : LegacyWrite());
api.MapPost("/personal", async (PersonalExpenseRequest request, FinanceService service, IConfiguration config, CancellationToken ct) =>
    Compatibility(config) ? Results.Ok(await service.PersonalAsync(request,ct)) : LegacyWrite());
api.MapPost("/fixed/convert", async (ConvertFixedRequest request, FinanceService service, IConfiguration config, CancellationToken ct) =>
    Compatibility(config) ? Results.Ok(await service.ConvertAsync(request,ct)) : LegacyWrite());
api.MapGet("/sync/status", (SyncService service, CancellationToken ct) => service.StatusAsync(ct));
api.MapGet("/sync/pull", (long after, SyncService service, CancellationToken ct) => service.PullAsync(after, ct));
api.MapPost("/sync/push", (SyncPushRequest request, SyncService service, CancellationToken ct) => service.PushAsync(request, ct));
api.MapPost("/sync/bootstrap", (BootstrapSyncRequest request, SyncService service, CancellationToken ct) => service.BootstrapAsync(request, ct));
api.MapPost("/sync/from-device", (DeviceSnapshotRequest request, DeviceSnapshotService service, CancellationToken ct) => service.ApplyAsync(request, ct));
api.MapGet("/sync/from-device/{operationId:guid}/backup", async (Guid operationId, DeviceSnapshotService service, CancellationToken ct) =>
    await service.BackupAsync(operationId, ct) is byte[] bytes ? Results.File(bytes, "application/octet-stream", "financeiro_before_device_sync.db") : Results.NotFound());
api.MapGet("/backup", async (BackupService service, CancellationToken ct) => Results.File(await service.ExportAsync(ct), "application/octet-stream", "financeiro.db"));
api.MapPost("/import/sqlite", ImportEndpoint.HandleAsync).DisableAntiforgery();
    }
private static bool Compatibility(IConfiguration config) => config.GetValue<bool>("LegacyWrites:CompatibilityMode");
private static async Task<IResult> LegacySaveAsync<T>(T item, FinanceService service, IConfiguration config, CancellationToken ct) where T:FinancialRecord =>
    Compatibility(config) ? Results.Ok(await service.SaveAsync(item,ct)) : LegacyWrite();
private static async Task<IResult> LegacyDeleteAsync<T>(int id, FinanceService service, IConfiguration config, CancellationToken ct) where T:FinancialRecord
{
    if (!Compatibility(config)) return LegacyWrite();
    await service.DeleteAsync<T>(id,ct);
    return Results.NoContent();
}
private static IResult LegacyWrite() => Results.Problem(
    "Atualize o aplicativo e envie as alterações por /api/commands, com identidade e revisão da operação.",
    statusCode: StatusCodes.Status428PreconditionRequired);
}
