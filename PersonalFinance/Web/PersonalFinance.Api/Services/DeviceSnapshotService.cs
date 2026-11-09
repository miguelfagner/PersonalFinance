using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Data;
using PersonalFinance.Contracts;

namespace PersonalFinance.Api.Services;

// Initial activation only: the complete device history is authoritative.
public sealed class DeviceSnapshotService(FinanceDbContext db)
{
    private const string Prefix = "device-snapshot:";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] Types = ["FixedIncome", "FixedExpense", "Income", "Expense", "Payment", "MonthlyPreparationItem"];
    private readonly Dictionary<(Type Type, Guid Id), int> parentIds = [];
    private sealed record Receipt(string Hash, byte[] Backup, SyncPullResult Result)
    {
        // The Android keeps decisions after deleting their fixed model. They no longer
        // affect preparation, but remain recoverable without inventing a replacement model.
        public IReadOnlyList<SyncChange> ArchivedDecisions { get; init; } = [];
    }

    public async Task<SyncPullResult> ApplyAsync(DeviceSnapshotRequest request, CancellationToken ct)
    {
        if (request.OperationId == Guid.Empty || request.Changes.Count is 0 or > 100000 ||
            request.Changes.Any(x => x.ClientId == Guid.Empty || x.Deleted || !x.Data.HasValue || !Types.Contains(x.EntityType)) ||
            request.Changes.Select(x => (x.EntityType, x.ClientId)).Distinct().Count() != request.Changes.Count)
            throw new ArgumentException("O histórico completo do celular deve conter registros válidos, sem identidades duplicadas.");
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, Json)));
        var existing = await db.SyncReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.OperationId, ct);
        if (existing is not null) return ReadReceipt(existing, hash).Result;
        var identities = request.Changes.Select(x => (x.EntityType, x.ClientId)).ToHashSet();
        var archivedDecisions = new List<SyncChange>();
        foreach (var change in request.Changes)
        {
            var parentType = change.EntityType switch
            {
                "FixedExpense" => "FixedIncome",
                "Expense" => "Income",
                "Payment" => "Expense",
                "MonthlyPreparationItem" => DecisionParentType(change),
                _ => null
            };
            if (parentType is not null && (!change.ParentClientId.HasValue || !identities.Contains((parentType, change.ParentClientId.Value))))
            {
                if (change.EntityType == "MonthlyPreparationItem") { archivedDecisions.Add(change); continue; }
                throw MissingParent(change, parentType, change.ParentClientId);
            }
            var fixedType = change.EntityType switch { "Income" => "FixedIncome", "Expense" => "FixedExpense", _ => null };
            if (fixedType is not null && change.FixedClientId.HasValue && !identities.Contains((fixedType, change.FixedClientId.Value)))
                throw MissingParent(change, fixedType, change.FixedClientId);
        }
        var archivedIds = archivedDecisions.Select(x => x.ClientId).ToHashSet();
        var activeChanges = request.Changes.Where(x => x.EntityType != "MonthlyPreparationItem" || !archivedIds.Contains(x.ClientId)).ToList();
        if (activeChanges.Count == 0) throw new ArgumentException("O histórico contém apenas decisões de modelos excluídos; nenhum lançamento foi enviado.");

        // Retain a restorable Android-format backup in SQL, in the same commit as the replacement.
        var backup = await new BackupService(db).ExportAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            if (db.Database.IsSqlServer())
                await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result=sp_getapplock @Resource=N'PersonalFinance.Sync',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=30000; IF @result<0 THROW 51000, 'Falha ao obter bloqueio de sincronização.', 1;", ct);
            existing = await db.SyncReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.OperationId, ct);
            if (existing is not null) return ReadReceipt(existing, hash).Result;
            var cursor = await db.SyncLogs.MaxAsync(x => (long?)x.Id, ct) ?? 0;
            if (cursor != request.ExpectedCursor)
                throw new InvalidOperationException("O servidor recebeu alterações durante a primeira carga. Toque em Sincronizar agora para tentar novamente.");

            var old = (await db.SyncLogs.AsNoTracking().ToListAsync(ct))
                .GroupBy(x => (x.EntityType, x.ClientId)).Select(x => x.MaxBy(y => y.Id)!).Where(x => !x.Deleted).ToList();
            var incoming = activeChanges.Select(x => (x.EntityType, x.ClientId)).ToHashSet();
            db.ChangeTracker.Clear();
            await db.MonthlyPreparationItems.ExecuteDeleteAsync(ct);
            await db.Payments.ExecuteDeleteAsync(ct);
            await db.Expenses.ExecuteDeleteAsync(ct);
            await db.Incomes.ExecuteDeleteAsync(ct);
            await db.FixedExpenses.ExecuteDeleteAsync(ct);
            await db.FixedIncomes.ExecuteDeleteAsync(ct);
            foreach (var removed in old.Where(x => !incoming.Contains((x.EntityType, x.ClientId))))
                db.SyncLogs.Add(new() { EntityType = removed.EntityType, ClientId = removed.ClientId, Deleted = true });
            await db.SaveChangesAsync(ct);

            parentIds.Clear();
            foreach (var type in Types)
            {
                foreach (var change in activeChanges.Where(x => x.EntityType == type))
                {
                    switch (type)
                    {
                        case "FixedIncome": await InsertAsync<FixedIncome>(change, ct); break;
                        case "FixedExpense": await InsertAsync<FixedExpense>(change, ct); break;
                        case "Income": await InsertAsync<Income>(change, ct); break;
                        case "Expense": await InsertAsync<Expense>(change, ct); break;
                        case "Payment": await InsertAsync<Payment>(change, ct); break;
                        case "MonthlyPreparationItem": await InsertAsync<MonthlyPreparationItem>(change, ct); break;
                    }
                }
                await db.SaveChangesAsync(ct);
                foreach (var entry in db.ChangeTracker.Entries<FinancialRecord>())
                    parentIds[(entry.Entity.GetType(), entry.Entity.ClientId)] = entry.Entity.Id;
            }
            var revisions = (await db.SyncLogs.AsNoTracking().Where(x => x.Id > cursor && !x.Deleted).ToListAsync(ct))
                .ToDictionary(x => (x.EntityType, x.ClientId), x => x.Id);
            var result = new SyncPullResult(await db.SyncLogs.MaxAsync(x => x.Id, ct),
                activeChanges.Select(x => x with { Revision = revisions[(x.EntityType, x.ClientId)] }).ToList(), false);
            db.SyncReceipts.Add(new() { Id = request.OperationId, Result = Prefix + JsonSerializer.Serialize(
                new Receipt(hash, backup, result) { ArchivedDecisions = archivedDecisions }, Json) });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<byte[]?> BackupAsync(Guid operationId, CancellationToken ct)
    {
        var receipt = await db.SyncReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == operationId, ct);
        return receipt?.Result.StartsWith(Prefix, StringComparison.Ordinal) == true
            ? JsonSerializer.Deserialize<Receipt>(receipt.Result[Prefix.Length..], Json)!.Backup : null;
    }

    private static Receipt ReadReceipt(SyncReceipt receipt, string hash)
    {
        if (!receipt.Result.StartsWith(Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("A operação já foi utilizada para outra alteração.");
        var saved = JsonSerializer.Deserialize<Receipt>(receipt.Result[Prefix.Length..], Json)!;
        if (saved.Hash != hash) throw new InvalidOperationException("A operação já foi utilizada para outro histórico.");
        return saved;
    }

    private static string DecisionParentType(SyncChange change)
    {
        var item = change.Data!.Value.Deserialize<MonthlyPreparationItem>(Json)!;
        if (item.ModelType is not ("INCOME" or "EXPENSE") || item.State is not ("GENERATED" or "IGNORED"))
            throw new ArgumentException("Decisão mensal inválida.");
        return item.ModelType == "INCOME" ? "FixedIncome" : "FixedExpense";
    }

    private static ArgumentException MissingParent(SyncChange change, string parentType, Guid? parentId)
    {
        var type = change.EntityType switch { "Expense" => "despesa", "Payment" => "pagamento", "FixedExpense" => "despesa fixa", _ => "receita" };
        var parent = parentType switch { "Income" => "receita", "Expense" => "despesa", "FixedIncome" => "receita fixa", _ => "despesa fixa" };
        var label = change.Data!.Value.TryGetProperty("description", out var description) ? description.GetString() :
            change.Data.Value.TryGetProperty("note", out var note) ? note.GetString() : "";
        return new($"Não foi possível vincular {type} '{label}' ({change.ClientId}) à {parent} " +
            $"({parentId?.ToString() ?? "sem identidade"}). Esse vínculo não consta na carga enviada pelo celular. Nenhum dado do servidor foi substituído.");
    }

    private async Task InsertAsync<T>(SyncChange change, CancellationToken ct) where T : FinancialRecord
    {
        var item = change.Data!.Value.Deserialize<T>(Json) ?? throw new ArgumentException("Dados ausentes.");
        item.Id = 0; item.ClientId = change.ClientId;
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(item, new(item), errors, true))
            throw new ArgumentException(string.Join(" ", errors.Select(x => x.ErrorMessage)));
        var amount = item switch { Income x => x.Amount, Expense x => x.Amount, Payment x => x.Amount, FixedIncome x => x.Amount, FixedExpense x => x.Amount, _ => 0m };
        if (amount < 0 || decimal.Round(amount, 2) != amount) throw new ArgumentException("Valor financeiro inválido no histórico do celular.");
        switch (item)
        {
            case Income x: x.FixedIncomeId = change.FixedClientId.HasValue ? await ParentAsync<FixedIncome>(change.FixedClientId, ct) : null; break;
            case FixedExpense x: x.FixedIncomeId = await ParentAsync<FixedIncome>(change.ParentClientId, ct); break;
            case Expense x:
                x.Payments = []; x.IncomeId = await ParentAsync<Income>(change.ParentClientId, ct);
                x.FixedExpenseId = change.FixedClientId.HasValue ? await ParentAsync<FixedExpense>(change.FixedClientId, ct) : null;
                break;
            case Payment x: x.ExpenseId = await ParentAsync<Expense>(change.ParentClientId, ct); break;
            case MonthlyPreparationItem x:
                if (x.ModelType is not ("INCOME" or "EXPENSE") || x.State is not ("GENERATED" or "IGNORED"))
                    throw new ArgumentException("Decisão mensal inválida.");
                x.ModelId = x.ModelType == "INCOME" ? await ParentAsync<FixedIncome>(change.ParentClientId, ct) : await ParentAsync<FixedExpense>(change.ParentClientId, ct);
                break;
        }
        db.Add(item);
    }

    private Task<int> ParentAsync<T>(Guid? id, CancellationToken ct) where T : FinancialRecord =>
        id.HasValue && parentIds.TryGetValue((typeof(T), id.Value), out var parent)
            ? Task.FromResult(parent) : throw new ArgumentException("O histórico do celular contém um vínculo ausente.");
}
