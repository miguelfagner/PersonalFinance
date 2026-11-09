using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Data;
using PersonalFinance.Contracts;

namespace PersonalFinance.Api.Services;

public sealed class SyncService(FinanceDbContext db, FinanceService finance)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<SyncStatus> StatusAsync(CancellationToken ct) => new(await db.Incomes.CountAsync(ct), await db.Expenses.CountAsync(ct), await db.Payments.CountAsync(ct), await db.FixedIncomes.CountAsync(ct), await db.FixedExpenses.CountAsync(ct),
        await db.Incomes.SumAsync(x => (decimal?)x.Amount, ct) ?? 0, await db.Expenses.SumAsync(x => (decimal?)x.Amount, ct) ?? 0, await db.Payments.SumAsync(x => (decimal?)x.Amount, ct) ?? 0, await db.SyncLogs.MaxAsync(x => (long?)x.Id, ct) ?? 0);

    public async Task<SyncPullResult> PullAsync(long after, CancellationToken ct)
    {
        if (after < 0) throw new ArgumentException("Cursor inválido.");
        var logs = await db.SyncLogs.AsNoTracking().Where(x => x.Id > after).OrderBy(x => x.Id).Take(501).ToListAsync(ct);
        var page = logs.Take(500).ToList();
        return new(page.LastOrDefault()?.Id ?? after, page.Select(x => new SyncChange(Guid.Empty, x.EntityType, x.ClientId, x.Deleted,
            x.Payload is null ? null : JsonSerializer.Deserialize<JsonElement>(x.Payload), x.ParentClientId, x.FixedClientId) { Revision = x.Id }).ToList(), logs.Count > 500);
    }

    public async Task<SyncPushResult> PushAsync(SyncPushRequest request, CancellationToken ct)
    {
        if (request.Changes.Count > 500) throw new ArgumentException("Envie no máximo 500 alterações por lote.");
        var accepted = new List<Guid>();
        var revisions = new Dictionary<Guid,long>();
        foreach (var change in request.Changes)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
                var receipt = await db.SyncReceipts.FindAsync([change.OperationId], ct);
                var fingerprint = Fingerprint(change);
                if (receipt is not null)
                {
                    var saved = receipt.Result.StartsWith("sync2:",StringComparison.Ordinal)
                        ? JsonSerializer.Deserialize<SyncReceiptResult>(receipt.Result[6..],Json) : null;
                    if (saved is not null)
                    {
                        if (saved.Hash != fingerprint) throw new InvalidOperationException("Este identificador de sincronização já foi usado por outra alteração.");
                        revisions[change.OperationId] = saved.Revision;
                    }
                    else if (receipt.Result != "sync" && receipt.Result != "sync:" + fingerprint)
                        throw new InvalidOperationException("Este identificador de sincronização já foi usado por outra alteração.");
                }
                if (receipt is null)
                {
                    var conflict = await ConflictAsync(change, ct);
                    if (conflict is not null)
                        return new(accepted, "O servidor possui uma versão mais recente. A alteração local foi preservada para consulta.")
                        { Conflicts = [conflict], Revisions = revisions };
                    await ApplyAsync(change, ct);
                    var revision = await db.SyncLogs.Where(x => x.EntityType == change.EntityType && x.ClientId == change.ClientId)
                        .MaxAsync(x => (long?)x.Id, ct) ?? 0;
                    db.SyncReceipts.Add(new() { Id = change.OperationId,
                        Result = "sync2:" + JsonSerializer.Serialize(new SyncReceiptResult(fingerprint,revision),Json) });
                    await db.SaveChangesAsync(ct);
                    revisions[change.OperationId] = revision;
                }
                await transaction.CommitAsync(ct); accepted.Add(change.OperationId);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateException)
            {
                db.ChangeTracker.Clear();
                return new(accepted, exception is DbUpdateException ? "Não foi possível aplicar a alteração. Atualize os vínculos e tente novamente." : exception.Message)
                    { Revisions = revisions };
            }
        }
        return new(accepted) { Revisions = revisions };
    }

    public async Task<SyncPushResult> BootstrapAsync(BootstrapSyncRequest request, CancellationToken ct)
    {
        if (request.Changes.Count > 100000) throw new ArgumentException("Histórico excede o limite da primeira carga.");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var bootstrapReceipt = await db.SyncReceipts.FindAsync([request.OperationId], ct);
        var bootstrapFingerprint = "bootstrap:" + Fingerprint(request);
        if (bootstrapReceipt is not null)
        {
            if (bootstrapReceipt.Result != "bootstrap" && bootstrapReceipt.Result != bootstrapFingerprint)
                throw new InvalidOperationException("Este identificador de primeira carga já foi usado com outro histórico.");
            return new(request.Changes.Select(x => x.OperationId).ToList());
        }
        var status = await StatusAsync(ct);
        if (status.Cursor != request.ExpectedCursor) throw new InvalidOperationException("O servidor mudou durante a revisão. Faça a conciliação novamente.");
        if (!request.Reconciled && (status.Incomes + status.Expenses + status.Payments + status.FixedIncomes + status.FixedExpenses > 0))
            throw new InvalidOperationException("O servidor já possui dados. Revise e concilie os dois históricos antes de ativar a sincronização.");
        foreach (var change in request.Changes)
        {
            var existingReceipt = await db.SyncReceipts.FindAsync([change.OperationId], ct);
            if (existingReceipt is not null)
            {
                if (existingReceipt.Result != "sync" && existingReceipt.Result != "sync:" + Fingerprint(change))
                    throw new InvalidOperationException("Uma alteração da primeira carga reutilizou uma identidade com outro conteúdo.");
                continue;
            }
            var existing = await db.SyncLogs.AnyAsync(x => x.EntityType == change.EntityType && x.ClientId == change.ClientId, ct);
            if (!existing) await ApplyAsync(change, ct);
            db.SyncReceipts.Add(new() { Id = change.OperationId, Result = "sync:" + Fingerprint(change) }); await db.SaveChangesAsync(ct);
        }
        db.SyncReceipts.Add(new() { Id = request.OperationId, Result = bootstrapFingerprint }); await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct); return new(request.Changes.Select(x => x.OperationId).ToList());
    }

    private static string Fingerprint<T>(T value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private sealed record SyncReceiptResult(string Hash,long Revision);

    private async Task ApplyAsync(SyncChange c, CancellationToken ct)
    {
        if (c.OperationId == Guid.Empty || c.ClientId == Guid.Empty) throw new ArgumentException("A operação e o lançamento precisam de uma identidade válida.");
        switch (c.EntityType)
        {
            case nameof(FixedIncome): await ApplyRecordAsync<FixedIncome>(c, ct); break;
            case nameof(FixedExpense): await ApplyRecordAsync<FixedExpense>(c, ct); break;
            case nameof(Income): await ApplyRecordAsync<Income>(c, ct); break;
            case nameof(Expense): await ApplyRecordAsync<Expense>(c, ct); break;
            case nameof(Payment): await ApplyRecordAsync<Payment>(c, ct); break;
            case nameof(MonthlyPreparationItem): await ApplyRecordAsync<MonthlyPreparationItem>(c, ct); break;
            default: throw new ArgumentException("Tipo de lançamento desconhecido.");
        }
    }
    private async Task<SyncConflict?> ConflictAsync(SyncChange change, CancellationToken ct)
    {
        var last = await db.SyncLogs.AsNoTracking().Where(x => x.EntityType == change.EntityType && x.ClientId == change.ClientId)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (last is null && change.ExpectedRevision is null or 0) return null;
        if (last?.Deleted == true && change.Deleted) return null;
        if (last is not null && !last.Deleted && change.ExpectedRevision == last.Id) return null;
        var current = last is null ? null : new SyncChange(Guid.Empty, last.EntityType, last.ClientId, last.Deleted,
            last.Payload is null ? null : JsonSerializer.Deserialize<JsonElement>(last.Payload), last.ParentClientId, last.FixedClientId)
            { Revision = last.Id };
        return new(change.OperationId, change.EntityType, change.ClientId,
            last?.Deleted == true ? "O lançamento foi excluído no servidor." : "O lançamento mudou no servidor.", current);
    }

    private async Task ApplyRecordAsync<T>(SyncChange c, CancellationToken ct) where T : FinancialRecord
    {
        var existing = await db.Set<T>().SingleOrDefaultAsync(x => x.ClientId == c.ClientId, ct);
        if (c.Deleted)
        {
            if (existing is not null) await finance.DeleteAsync<T>(existing.Id, ct);
            else if (!await db.SyncLogs.AnyAsync(x => x.EntityType == c.EntityType && x.ClientId == c.ClientId && x.Deleted, ct))
            { db.SyncLogs.Add(new() { EntityType = c.EntityType, ClientId = c.ClientId, Deleted = true }); await db.SaveChangesAsync(ct); }
            return;
        }
        if (await db.SyncLogs.Where(x => x.EntityType == c.EntityType && x.ClientId == c.ClientId)
            .OrderByDescending(x => x.Id).Select(x => x.Deleted).FirstOrDefaultAsync(ct)) throw new InvalidOperationException("Este registro foi excluído no outro aplicativo. Crie um novo lançamento.");
        var item = c.Data?.Deserialize<T>(Json) ?? throw new ArgumentException("Dados do lançamento ausentes.");
        item.Id = existing?.Id ?? 0; item.ClientId = c.ClientId;
        switch (item)
        {
            case Income i: i.FixedIncomeId = c.FixedClientId.HasValue ? await ParentIdAsync<FixedIncome>(c.FixedClientId, ct) : null; break;
            case Expense e: e.Payments = []; e.IncomeId = await ParentIdAsync<Income>(c.ParentClientId, ct); e.FixedExpenseId = c.FixedClientId.HasValue ? await ParentIdAsync<FixedExpense>(c.FixedClientId, ct) : null; break;
            case Payment p: p.ExpenseId = await ParentIdAsync<Expense>(c.ParentClientId, ct); break;
            case FixedExpense f: f.FixedIncomeId = await ParentIdAsync<FixedIncome>(c.ParentClientId, ct); break;
            case MonthlyPreparationItem m:
                if (m.ModelType is not ("INCOME" or "EXPENSE") || m.State is not ("GENERATED" or "IGNORED")) throw new ArgumentException("Decisão mensal inválida.");
                m.ModelId = m.ModelType == "INCOME" ? await ParentIdAsync<FixedIncome>(c.ParentClientId, ct) : await ParentIdAsync<FixedExpense>(c.ParentClientId, ct);
                m.ReferenceMonth = FinanceService.Month(m.ReferenceMonth);
                var decision = await db.MonthlyPreparationItems.SingleOrDefaultAsync(x => x.ModelType == m.ModelType && x.ModelId == m.ModelId && x.ReferenceMonth == m.ReferenceMonth, ct);
                // The semantic key also identifies decisions created independently on both devices.
                if (decision is not null) { m.Id = decision.Id; m.ClientId = decision.ClientId; }
                break;
        }
        await finance.SaveAsync(item, ct);
    }
    private async Task<int> ParentIdAsync<T>(Guid? id, CancellationToken ct) where T : FinancialRecord =>
        (await db.Set<T>().SingleOrDefaultAsync(x => x.ClientId == id, ct))?.Id ?? throw new ArgumentException("O lançamento vinculado ainda não foi sincronizado ou foi excluído.");
}
