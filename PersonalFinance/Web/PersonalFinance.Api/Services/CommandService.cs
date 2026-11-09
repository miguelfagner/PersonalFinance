using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Data;
using PersonalFinance.Contracts;

namespace PersonalFinance.Api.Services;

// One durable operation identity is used for an immediate request and every retry.
public sealed class CommandService(FinanceDbContext db, FinanceService finance)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<FinanceCommandResult> ExecuteAsync(FinanceCommand command, CancellationToken ct)
    {
        if (command.OperationId == Guid.Empty) throw new ArgumentException("Identificador da operação inválido.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command, Json))));
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var receipt = await db.SyncReceipts.FindAsync([command.OperationId], ct);
        if (receipt is not null)
        {
            var saved = receipt.Result.StartsWith("command:", StringComparison.Ordinal)
                ? JsonSerializer.Deserialize<CommandReceipt>(receipt.Result[8..], Json) : null;
            if (saved is null || saved.Hash != hash) throw new InvalidOperationException("Este identificador já foi usado por outra operação.");
            return saved.Result;
        }

        if (command.DependsOn.HasValue && !await db.SyncReceipts.AnyAsync(x => x.Id == command.DependsOn.Value, ct))
            throw new InvalidOperationException("A operação anterior ainda não foi confirmada.");

        var before = await db.SyncLogs.MaxAsync(x => (long?)x.Id, ct) ?? 0;
        var result = await ApplyAsync(command, ct);
        var revisions = await db.SyncLogs.AsNoTracking().Where(x => x.Id > before && !x.Deleted)
            .OrderBy(x => x.Id).Select(x => new { x.ClientId, x.Id }).ToListAsync(ct);
        result = result with { Revisions = revisions.GroupBy(x => x.ClientId)
            .ToDictionary(x => x.Key, x => x.Max(y => y.Id)) };
        db.SyncReceipts.Add(new() { Id = command.OperationId,
            Result = "command:" + JsonSerializer.Serialize(new CommandReceipt(hash, result), Json) });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private async Task<FinanceCommandResult> ApplyAsync(FinanceCommand command, CancellationToken ct)
    {
        if (command.Kind is "Upsert" or "Delete")
        {
            if (command.ClientId == Guid.Empty) throw new ArgumentException("Identificador do lançamento inválido.");
            var current = await CurrentAsync(command.EntityType, command.ClientId, ct);
            if (command.Kind == "Delete" && current is null) return new("Applied");
            var tombstone = current is null && await db.SyncLogs.AnyAsync(x => x.EntityType == command.EntityType && x.ClientId == command.ClientId && x.Deleted, ct);
            if (current is null ? tombstone || command.ExpectedRevision is > 0 : command.ExpectedRevision != current.Revision)
                return new("Conflict", Error: "O lançamento mudou no servidor. A alteração local foi preservada para consulta.",
                    Current: await CurrentChangeAsync(command.EntityType, command.ClientId, current, ct));
            if (command.Kind == "Delete")
            {
                await DeleteAsync(command.EntityType, current!.Id, ct);
                return new("Applied");
            }
            var updated = await UpsertAsync(command, current, ct);
            return new("Applied", JsonSerializer.SerializeToElement(updated, updated.GetType(), Json));
        }

        if (command.Kind == "Prepare")
        {
            var request = Required<PrepareMonthRequest>(command.Data) with { OperationId = Derived(command.OperationId, "prepare") };
            var result = await finance.PrepareAsync(request, ct);
            return new("Applied", JsonSerializer.SerializeToElement(result, Json));
        }
        if (command.Kind == "PrepareIdentity")
        {
            var request = Required<PrepareIdentityRequest>(command.Data);
            var items = new List<PreparationPreviewItem>();
            foreach (var source in request.Items)
            {
                var modelId = source.ModelType == "INCOME"
                    ? await ResolveAsync<FixedIncome>(source.ModelClientId, ct)
                    : source.ModelType == "EXPENSE"
                        ? await ResolveAsync<FixedExpense>(source.ModelClientId, ct)
                        : throw new ArgumentException("Tipo de modelo mensal inválido.");
                items.Add(new() { ModelType=source.ModelType, ModelId=modelId, Selected=source.Selected,
                    Payer=source.Payer, Description=source.Description, Type=source.Type,
                    Category=source.Category, Amount=source.Amount, DueDay=source.DueDay });
            }
            var prepared = new PrepareMonthRequest(request.Month, [], [])
            { OperationId=Derived(command.OperationId, "prepare"), Items=items };
            var result = await finance.PrepareAsync(prepared, ct);
            return new("Applied", JsonSerializer.SerializeToElement(result, Json));
        }
        if (command.Kind == "Settle")
        {
            var expenseId = await ResolveAsync<Expense>(command.ClientId, ct);
            var request = Required<SettleRequest>(command.Data) with { OperationId = Derived(command.OperationId, "settle") };
            var result = await finance.SettleAsync(expenseId, request, ct);
            return new("Applied", JsonSerializer.SerializeToElement(result, Json));
        }
        if (command.Kind == "Personal")
        {
            var request = Required<PersonalExpenseRequest>(command.Data) with
            {
                ExpenseId = await ResolveAsync<Expense>(command.ParentClientId, ct), ClientId = command.ClientId
            };
            var result = await finance.PersonalAsync(request, ct);
            return new("Applied", JsonSerializer.SerializeToElement(result, Json));
        }
        if (command.Kind == "Convert")
        {
            var result = await finance.ConvertAsync(Required<ConvertFixedRequest>(command.Data), ct);
            return new("Applied", JsonSerializer.SerializeToElement(result, Json));
        }
        if (command.Kind == "ConvertIdentity")
        {
            var request = Required<ConvertIdentityRequest>(command.Data);
            var incomes = new List<int>();
            foreach (var id in request.IncomeClientIds) incomes.Add(await ResolveAsync<Income>(id, ct));
            var expenses = new List<int>();
            foreach (var id in request.ExpenseClientIds) expenses.Add(await ResolveAsync<Expense>(id, ct));
            var result = await finance.ConvertAsync(new(request.Month, incomes, expenses), ct);
            return new("Applied", JsonSerializer.SerializeToElement(result, Json));
        }
        throw new ArgumentException("Tipo de operação desconhecido.");
    }

    private async Task<FinancialRecord> UpsertAsync(FinanceCommand command, FinancialRecord? current, CancellationToken ct)
    {
        switch (command.EntityType)
        {
            case nameof(Income):
                var income = Required<Income>(command.Data);
                income.Id = current?.Id ?? 0; income.ClientId = command.ClientId;
                if (command.FixedClientId.HasValue) income.FixedIncomeId = await ResolveAsync<FixedIncome>(command.FixedClientId, ct);
                return await finance.SaveAsync(income, ct);
            case nameof(Expense):
                var expense = Required<Expense>(command.Data);
                expense.Id = current?.Id ?? 0; expense.ClientId = command.ClientId; expense.Payments = [];
                if (command.ParentClientId.HasValue) expense.IncomeId = await ResolveAsync<Income>(command.ParentClientId, ct);
                if (command.FixedClientId.HasValue) expense.FixedExpenseId = await ResolveAsync<FixedExpense>(command.FixedClientId, ct);
                return await finance.SaveAsync(expense, ct);
            case nameof(Payment):
                var payment = Required<Payment>(command.Data);
                payment.Id = current?.Id ?? 0; payment.ClientId = command.ClientId;
                if (command.ParentClientId.HasValue) payment.ExpenseId = await ResolveAsync<Expense>(command.ParentClientId, ct);
                return await finance.SaveAsync(payment, ct);
            case nameof(FixedIncome):
                var fixedIncome = Required<FixedIncome>(command.Data);
                fixedIncome.Id = current?.Id ?? 0; fixedIncome.ClientId = command.ClientId;
                return await finance.SaveAsync(fixedIncome, ct);
            case nameof(FixedExpense):
                var fixedExpense = Required<FixedExpense>(command.Data);
                fixedExpense.Id = current?.Id ?? 0; fixedExpense.ClientId = command.ClientId;
                if (command.ParentClientId.HasValue) fixedExpense.FixedIncomeId = await ResolveAsync<FixedIncome>(command.ParentClientId, ct);
                return await finance.SaveAsync(fixedExpense, ct);
            case nameof(MonthlyPreparationItem):
                var decision = Required<MonthlyPreparationItem>(command.Data);
                decision.Id = current?.Id ?? 0; decision.ClientId = command.ClientId;
                if (command.ParentClientId.HasValue)
                    decision.ModelId = decision.ModelType == "INCOME"
                        ? await ResolveAsync<FixedIncome>(command.ParentClientId, ct)
                        : await ResolveAsync<FixedExpense>(command.ParentClientId, ct);
                return await finance.SaveAsync(decision, ct);
            default: throw new ArgumentException("Tipo de lançamento desconhecido.");
        }
    }

    private Task<FinancialRecord?> CurrentAsync(string type, Guid id, CancellationToken ct) => type switch
    {
        nameof(Income) => FindAsync<Income>(id, ct),
        nameof(Expense) => FindAsync<Expense>(id, ct),
        nameof(Payment) => FindAsync<Payment>(id, ct),
        nameof(FixedIncome) => FindAsync<FixedIncome>(id, ct),
        nameof(FixedExpense) => FindAsync<FixedExpense>(id, ct),
        nameof(MonthlyPreparationItem) => FindAsync<MonthlyPreparationItem>(id, ct),
        _ => throw new ArgumentException("Tipo de lançamento desconhecido.")
    };

    private async Task<FinancialRecord?> FindAsync<T>(Guid id, CancellationToken ct) where T : FinancialRecord =>
        await db.Set<T>().SingleOrDefaultAsync(x => x.ClientId == id, ct);

    private async Task<SyncChange?> CurrentChangeAsync(string type, Guid id, FinancialRecord? current, CancellationToken ct)
    {
        if (current is null)
        {
            var deleted = await db.SyncLogs.AsNoTracking().Where(x => x.EntityType == type && x.ClientId == id && x.Deleted)
                .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            return deleted is null ? null : new(Guid.Empty, type, id, true, null) { Revision = deleted.Id };
        }
        Guid? parent = current switch
        {
            Expense x => await db.Incomes.Where(p => p.Id == x.IncomeId).Select(p => (Guid?)p.ClientId).FirstOrDefaultAsync(ct),
            Payment x => await db.Expenses.Where(p => p.Id == x.ExpenseId).Select(p => (Guid?)p.ClientId).FirstOrDefaultAsync(ct),
            FixedExpense x => await db.FixedIncomes.Where(p => p.Id == x.FixedIncomeId).Select(p => (Guid?)p.ClientId).FirstOrDefaultAsync(ct),
            MonthlyPreparationItem x when x.ModelType == "INCOME" => await db.FixedIncomes.Where(p => p.Id == x.ModelId).Select(p => (Guid?)p.ClientId).FirstOrDefaultAsync(ct),
            MonthlyPreparationItem x => await db.FixedExpenses.Where(p => p.Id == x.ModelId).Select(p => (Guid?)p.ClientId).FirstOrDefaultAsync(ct),
            _ => null
        };
        Guid? fixedId = current switch
        {
            Income x when x.FixedIncomeId.HasValue => await db.FixedIncomes.Where(p => p.Id == x.FixedIncomeId).Select(p => (Guid?)p.ClientId).FirstOrDefaultAsync(ct),
            Expense x when x.FixedExpenseId.HasValue => await db.FixedExpenses.Where(p => p.Id == x.FixedExpenseId).Select(p => (Guid?)p.ClientId).FirstOrDefaultAsync(ct),
            _ => null
        };
        return new(Guid.Empty, type, id, false, JsonSerializer.SerializeToElement(current, current.GetType(), Json), parent, fixedId)
        { Revision = current.Revision };
    }

    private Task DeleteAsync(string type, int id, CancellationToken ct) => type switch
    {
        nameof(Income) => finance.DeleteAsync<Income>(id, ct),
        nameof(Expense) => finance.DeleteAsync<Expense>(id, ct),
        nameof(Payment) => finance.DeleteAsync<Payment>(id, ct),
        nameof(FixedIncome) => finance.DeleteAsync<FixedIncome>(id, ct),
        nameof(FixedExpense) => finance.DeleteAsync<FixedExpense>(id, ct),
        nameof(MonthlyPreparationItem) => finance.DeleteAsync<MonthlyPreparationItem>(id, ct),
        _ => throw new ArgumentException("Tipo de lançamento desconhecido.")
    };

    private async Task<int> ResolveAsync<T>(Guid? id, CancellationToken ct) where T : FinancialRecord =>
        id.HasValue ? (await db.Set<T>().Where(x => x.ClientId == id.Value).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct)
            ?? throw new ArgumentException("O lançamento vinculado não existe no servidor."))
            : throw new ArgumentException("O lançamento vinculado não foi informado.");

    private static T Required<T>(JsonElement? data) where T : class => data.HasValue
        ? data.Value.Deserialize<T>(Json) ?? throw new ArgumentException("Dados da operação inválidos.")
        : throw new ArgumentException("Dados da operação ausentes.");

    private static Guid Derived(Guid operation, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(purpose + ":" + operation));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private sealed record CommandReceipt(string Hash, FinanceCommandResult Result);
}
