using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Data;
using PersonalFinance.Contracts;
using System.Text.Json;

namespace PersonalFinance.Api.Services;

public sealed class FinanceService(FinanceDbContext db)
{
    public async Task<T> SaveAsync<T>(T entity, CancellationToken ct) where T : FinancialRecord
    {
        var existing = entity.Id > 0 ? await db.Set<T>().FindAsync([entity.Id], ct) : await db.Set<T>().SingleOrDefaultAsync(x => x.ClientId == entity.ClientId, ct);
        var previousExpenseId = existing is Payment previousPayment ? previousPayment.ExpenseId : 0;
        if (entity.Id == 0 && existing is not null) return existing;
        if (entity.Id > 0 && existing is null) throw new KeyNotFoundException("Registro não encontrado.");
        if (await db.SyncLogs.Where(x => x.EntityType == typeof(T).Name && x.ClientId == entity.ClientId)
            .OrderByDescending(x => x.Id).Select(x => x.Deleted).FirstOrDefaultAsync(ct))
            throw new InvalidOperationException("Este registro foi excluído. Crie um novo lançamento.");
        if (existing is not null) { entity.ClientId = existing.ClientId; entity.CreatedAtUtc = existing.CreatedAtUtc; }
        Validate(entity);
        switch (entity)
        {
            case Income i:
                i.ReferenceMonth = Month(i.ReferenceMonth);
                if (existing is Income oldI && oldI.GeneratedForMonth is DateOnly generatedI)
                {
                    if (i.ReferenceMonth != generatedI) throw new ArgumentException("A competência de uma receita gerada não pode ser alterada.");
                    i.FixedIncomeId = oldI.FixedIncomeId; i.GeneratedForMonth = generatedI;
                }
                break;
            case Expense e:
                if (e.IncomeId == 0 && e.IncomeClientId.HasValue) e.IncomeId = (await db.Incomes.SingleOrDefaultAsync(x => x.ClientId == e.IncomeClientId, ct))?.Id ?? 0;
                if (!await db.Incomes.AnyAsync(x => x.Id == e.IncomeId, ct)) throw new ArgumentException("Selecione uma receita válida.");
                if (existing is Expense oldE && oldE.GeneratedForMonth is DateOnly generatedE)
                {
                    if (Month(e.DueDate) != generatedE) throw new ArgumentException("A competência de uma despesa gerada não pode ser alterada.");
                    e.IncomeId = oldE.IncomeId; e.CreatedOn = oldE.CreatedOn; e.InstallmentNumber = oldE.InstallmentNumber;
                    e.Replicable = false; e.FixedExpenseId = oldE.FixedExpenseId; e.GeneratedForMonth = generatedE;
                }
                e.Amount = Math.Max(e.Amount, await db.Payments.Where(x => x.ExpenseId == e.Id && e.Id > 0).SumAsync(x => (decimal?)x.Amount, ct) ?? 0);
                break;
            case Payment p:
                if (p.ExpenseId == 0 && p.ExpenseClientId.HasValue) p.ExpenseId = (await db.Expenses.SingleOrDefaultAsync(x => x.ClientId == p.ExpenseClientId, ct))?.Id ?? 0;
                if (!await db.Expenses.AnyAsync(x => x.Id == p.ExpenseId, ct)) throw new ArgumentException("Selecione uma despesa válida.");
                break;
            case FixedExpense f:
                var parent = await db.FixedIncomes.FindAsync([f.FixedIncomeId], ct) ?? throw new ArgumentException("Selecione uma receita fixa válida.");
                if (f.Active && !parent.Active) throw new ArgumentException("Reative a receita fixa antes de ativar esta despesa.");
                if (f.DueDay is < 1 or > 31) throw new ArgumentException("O dia de vencimento deve estar entre 1 e 31.");
                break;
        }
        if (existing is null) db.Add(entity); else db.Entry(existing).CurrentValues.SetValues(entity);
        if (entity is FixedIncome { Active: false } paused)
            foreach (var child in await db.FixedExpenses.Where(x => x.FixedIncomeId == paused.Id && x.Active).ToListAsync(ct)) child.Active = false;
        if (entity is Payment payment)
        {
            var expense = await db.Expenses.FindAsync([payment.ExpenseId], ct);
            var total = (await db.Payments.Where(x => x.ExpenseId == payment.ExpenseId && x.Id != payment.Id).SumAsync(x => (decimal?)x.Amount, ct) ?? 0) + payment.Amount;
            if (expense is not null)
            {
                if (total > expense.Amount) expense.Amount = total;
                db.Entry(expense).State = EntityState.Modified;
            }
            if (previousExpenseId > 0 && previousExpenseId != payment.ExpenseId)
            {
                var previousExpense = await db.Expenses.FindAsync([previousExpenseId], ct);
                if (previousExpense is not null) db.Entry(previousExpense).State = EntityState.Modified;
            }
        }
        await db.SaveChangesAsync(ct);
        return existing ?? entity;
    }

    public async Task DeleteAsync<T>(int id, CancellationToken ct) where T : FinancialRecord
    {
        var item = await db.Set<T>().FindAsync([id], ct) ?? throw new KeyNotFoundException("Registro não encontrado.");
        if (item is Income && await db.Expenses.AnyAsync(x => x.IncomeId == id, ct)) throw new ArgumentException("Esta receita possui despesas vinculadas. Exclua ou mova as despesas antes de continuar.");
        if (item is FixedIncome && await db.FixedExpenses.AnyAsync(x => x.FixedIncomeId == id, ct)) throw new ArgumentException("Esta receita fixa possui despesas vinculadas. Exclua as despesas antes de continuar.");
        if (item is Expense) db.Payments.RemoveRange(await db.Payments.Where(x => x.ExpenseId == id).ToListAsync(ct));
        if (item is Payment payment)
        {
            var expense = await db.Expenses.FindAsync([payment.ExpenseId], ct);
            if (expense is not null) db.Entry(expense).State = EntityState.Modified;
        }
        db.Remove(item); await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ExpenseListItem>> ExpensesAsync(DateOnly month, CancellationToken ct)
    {
        var start = Month(month); var end = start.AddMonths(1);
        var expenses = await db.Expenses.AsNoTracking().Include(x => x.Payments).Where(x => x.DueDate >= start && x.DueDate < end).OrderBy(x => x.DueDate).ToListAsync(ct);
        return expenses.Select(x => { var paid = x.Payments.Sum(p => p.Amount); var remaining = Math.Max(0, x.Amount - paid); return new ExpenseListItem(x, paid, remaining, remaining == 0 ? "Quitada" : paid > 0 ? "Parcial" : "Pendente"); }).ToList();
    }

    public async Task<IReadOnlyList<PaymentListItem>> PaymentsAsync(DateOnly month, int? expenseId, string? category, int? week, CancellationToken ct, IReadOnlyCollection<string>? categories = null, bool personalDueMonth = false)
    {
        var start = Month(month); var end = start.AddMonths(1);
        var query = from p in db.Payments.AsNoTracking() join e in db.Expenses on p.ExpenseId equals e.Id join i in db.Incomes on e.IncomeId equals i.Id
                    where p.Date >= start && p.Date < end && (!expenseId.HasValue || p.ExpenseId == expenseId)
                    orderby p.Date descending, p.Id descending select new PaymentListItem(p, e.Description, e.Category, i.Payer, e.DueDate);
        var items = await query.ToListAsync(ct);
        var selectedCategories = categories?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(BalanceCalculator.NormalizeCategory).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return items.Where(x => (string.IsNullOrWhiteSpace(category) || BalanceCalculator.NormalizeCategory(x.Category) == BalanceCalculator.NormalizeCategory(category)) &&
            (selectedCategories is not { Count: > 0 } || selectedCategories.Contains(BalanceCalculator.NormalizeCategory(x.Category))) &&
            (!personalDueMonth || (BalanceCalculator.NormalizeCategory(x.Category) == "PESSOAL" && Month(x.DueDate) == start)) &&
            (!week.HasValue || (BalanceCalculator.NormalizeCategory(x.Category) == "PESSOAL" && Month(x.DueDate) == start && (x.Payment.Date.Day + (int)start.DayOfWeek - 1) / 7 + 1 == week))).ToList();
    }

    public async Task<BalanceSummary> BalanceAsync(DateOnly month, CancellationToken ct, DateOnly? today = null)
    {
        var start = Month(month); var end = start.AddMonths(1);
        var incomes = await db.Incomes.AsNoTracking().Where(x => x.ReferenceMonth >= start && x.ReferenceMonth < end).ToListAsync(ct);
        var expenses = await db.Expenses.AsNoTracking().Include(x => x.Payments).Where(x => (x.DueDate >= start && x.DueDate < end) || x.Payments.Any(p => p.Date >= start && p.Date < end)).ToListAsync(ct);
        return BalanceCalculator.Calculate(start, incomes, expenses, today ?? DateOnly.FromDateTime(DateTime.Today));
    }

    public async Task<IReadOnlyList<PreparationPreviewItem>> PreviewAsync(DateOnly month, CancellationToken ct)
    {
        month = Month(month);
        var incomes = await db.FixedIncomes.Where(x => x.Active).ToListAsync(ct);
        var parentIds = incomes.Select(x => x.Id).ToList();
        var expenses = await db.FixedExpenses.Where(x => x.Active && parentIds.Contains(x.FixedIncomeId)).ToListAsync(ct);
        var generatedIncomes = await db.Incomes.Where(x => x.GeneratedForMonth == month && x.FixedIncomeId != null).ToListAsync(ct);
        var generatedExpenses = await db.Expenses.Where(x => x.GeneratedForMonth == month && x.FixedExpenseId != null).ToListAsync(ct);
        var decisions = await db.MonthlyPreparationItems.Where(x => x.ReferenceMonth == month).ToListAsync(ct);
        var prepared = decisions.Count > 0 || generatedIncomes.Count > 0 || generatedExpenses.Count > 0;
        var items = new List<PreparationPreviewItem>();
        foreach (var model in incomes)
        {
            var generated = generatedIncomes.SingleOrDefault(x => x.FixedIncomeId == model.Id);
            var decision = decisions.SingleOrDefault(x => x.ModelType == "INCOME" && x.ModelId == model.Id);
            items.Add(new() { ModelType = "INCOME", ModelId = model.Id, Payer = generated?.Payer ?? model.Payer, Description = generated?.Description ?? model.Description, Type = generated?.Type ?? model.Type, Amount = generated?.Amount ?? model.Amount, AlreadyCreated = generated is not null, Ignored = generated is null && decision?.State == "IGNORED", Selected = generated is null && decision?.State != "IGNORED", NewSincePreparation = prepared && generated is null && decision is null });
        }
        foreach (var model in expenses)
        {
            var generated = generatedExpenses.SingleOrDefault(x => x.FixedExpenseId == model.Id);
            var decision = decisions.SingleOrDefault(x => x.ModelType == "EXPENSE" && x.ModelId == model.Id);
            items.Add(new() { ModelType = "EXPENSE", ModelId = model.Id, FixedIncomeId = model.FixedIncomeId, Description = generated?.Description ?? model.Description, Category = generated?.Category ?? model.Category, Amount = generated?.Amount ?? model.Amount, DueDay = generated?.DueDate.Day ?? model.DueDay, AlreadyCreated = generated is not null, Ignored = generated is null && decision?.State == "IGNORED", Selected = generated is null && decision?.State != "IGNORED", NewSincePreparation = prepared && generated is null && decision is null });
        }
        return items;
    }

    public async Task<PreparationResult> PrepareAsync(PrepareMonthRequest request, CancellationToken ct)
    {
        var receipt = await db.SyncReceipts.FindAsync([request.OperationId], ct);
        if (receipt is not null) return JsonSerializer.Deserialize<PreparationResult>(receipt.Result)!;
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var month = Month(request.Month);
        var preview = await PreviewAsync(month, ct);
        var items = request.Items ?? preview.Select(x => { x.Selected = x.ModelType == "INCOME" ? request.FixedIncomeIds.Contains(x.ModelId) : request.FixedExpenseIds.Contains(x.ModelId); return x; }).ToList();
        var ic = 0; var ie = 0; var ec = 0; var ee = 0; var ignored = 0;
        foreach (var item in items.OrderBy(x => x.ModelType == "INCOME" ? 0 : 1))
        {
            var model = preview.SingleOrDefault(x => x.ModelType == item.ModelType && x.ModelId == item.ModelId) ?? throw new ArgumentException("Um modelo foi removido ou pausado. Atualize a revisão do mês.");
            var decision = await db.MonthlyPreparationItems.SingleOrDefaultAsync(x => x.ModelType == item.ModelType && x.ModelId == item.ModelId && x.ReferenceMonth == month, ct);
            if (decision is null) { decision = new() { ModelType = item.ModelType, ModelId = item.ModelId, ReferenceMonth = month }; db.Add(decision); }
            if (model.AlreadyCreated) { decision.State = "GENERATED"; if (item.ModelType == "INCOME") ie++; else ee++; continue; }
            decision.State = item.Selected ? "GENERATED" : "IGNORED";
            if (!item.Selected) { ignored++; continue; }
            if (item.Amount <= 0) throw new ArgumentException("Informe um valor maior que zero.");
            if (item.ModelType == "INCOME")
            {
                var incomeModel = await db.FixedIncomes.FindAsync([item.ModelId], ct) ?? throw new ArgumentException("Modelo não encontrado.");
                db.Incomes.Add(new() { ClientId = await SnapshotIdentityAsync("Income", incomeModel.ClientId, month, ct), ReferenceMonth = month, FixedIncomeId = item.ModelId, GeneratedForMonth = month, Payer = item.Payer, Description = item.Description, Type = item.Type, Amount = item.Amount }); ic++;
                await db.SaveChangesAsync(ct);
            }
            else
            {
                if (item.DueDay is < 1 or > 31) throw new ArgumentException("Informe um dia de vencimento entre 1 e 31.");
                var parent = await db.Incomes.SingleOrDefaultAsync(x => x.FixedIncomeId == model.FixedIncomeId && x.GeneratedForMonth == month, ct) ?? throw new ArgumentException("Uma despesa fixa não pode ser gerada sem sua receita mensal.");
                var expenseModel = await db.FixedExpenses.FindAsync([item.ModelId], ct) ?? throw new ArgumentException("Modelo não encontrado.");
                db.Expenses.Add(new() { ClientId = await SnapshotIdentityAsync("Expense", expenseModel.ClientId, month, ct), IncomeId = parent.Id, CreatedOn = DateOnly.FromDateTime(DateTime.Today), DueDate = month.AddDays(Math.Min(item.DueDay, DateTime.DaysInMonth(month.Year, month.Month)) - 1), FixedExpenseId = item.ModelId, GeneratedForMonth = month, Description = item.Description, Category = item.Category, Amount = item.Amount }); ec++;
            }
        }
        var result = new PreparationResult(ic, ie, ec, ee, ignored);
        db.SyncReceipts.Add(new() { Id = request.OperationId, Result = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return result;
    }

    public async Task<Payment> SettleAsync(int expenseId, SettleRequest request, CancellationToken ct)
    {
        var already = await db.Payments.SingleOrDefaultAsync(x => x.ClientId == request.OperationId, ct);
        if (already is not null) return already;
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var expense = await db.Expenses.FindAsync([expenseId], ct) ?? throw new KeyNotFoundException("Despesa não encontrada.");
        var remaining = expense.Amount - (await db.Payments.Where(x => x.ExpenseId == expenseId).SumAsync(x => (decimal?)x.Amount, ct) ?? 0);
        if (remaining <= 0) throw new ArgumentException("A despesa já está quitada.");
        var result = await SaveAsync(new Payment { ClientId = request.OperationId, ExpenseId = expenseId, Amount = remaining, Date = request.Date, Note = request.Note }, ct);
        if (tx is not null) await tx.CommitAsync(ct); return result;
    }

    public async Task<Payment> PersonalAsync(PersonalExpenseRequest request, CancellationToken ct)
    {
        var expense = await db.Expenses.FindAsync([request.ExpenseId], ct) ?? throw new ArgumentException("O orçamento selecionado não existe mais.");
        if (BalanceCalculator.NormalizeCategory(expense.Category) != "PESSOAL" || Month(expense.DueDate) != Month(request.Date)) throw new ArgumentException("Selecione um orçamento PESSOAL do mesmo mês do gasto.");
        if (string.IsNullOrWhiteSpace(request.Note)) throw new ArgumentException("Informe a descrição do gasto.");
        return await SaveAsync(new Payment { ClientId = request.ClientId, ExpenseId = expense.Id, Date = request.Date, Note = request.Note, Amount = request.Amount }, ct);
    }

    public async Task<ConvertFixedResult> ConvertAsync(ConvertFixedRequest request, CancellationToken ct)
    {
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var month = Month(request.Month); var end = month.AddMonths(1);
        var expenses = await db.Expenses.Where(x => request.ExpenseIds.Contains(x.Id) && x.DueDate >= month && x.DueDate < end).ToListAsync(ct);
        var incomeIds = request.IncomeIds.Concat(expenses.Select(x => x.IncomeId)).Distinct().ToList();
        var incomes = await db.Incomes.Where(x => incomeIds.Contains(x.Id)).ToListAsync(ct);
        var targets = new Dictionary<int, Income>();
        var ic = 0; var ir = 0; var ec = 0; var er = 0;
        foreach (var income in incomes)
        {
            var model = income.FixedIncomeId.HasValue ? await db.FixedIncomes.FindAsync([income.FixedIncomeId.Value], ct) : null;
            if (model is null) { model = new() { ClientId=FinanceIdentity.ForSource("FixedIncome",income.ClientId), Payer = income.Payer, Description = income.Description, Type = income.Type, Amount = income.Amount }; await SaveAsync(model, ct); ic++; } else ir++;
            var original = Month(income.ReferenceMonth);
            var snapshot = await db.Incomes.SingleOrDefaultAsync(x => x.FixedIncomeId == model.Id && x.GeneratedForMonth == original, ct);
            if (snapshot is null || snapshot.Id == income.Id) { income.FixedIncomeId = model.Id; income.GeneratedForMonth = original; income.ReferenceMonth = original; snapshot = income; }
            await db.SaveChangesAsync(ct);
            var target = original == month ? snapshot : await db.Incomes.SingleOrDefaultAsync(x => x.FixedIncomeId == model.Id && x.GeneratedForMonth == month, ct);
            if (target is null) { target = new() { ClientId=await SnapshotIdentityAsync("Income",model.ClientId,month,ct), Payer = model.Payer, Description = model.Description, Type = model.Type, Amount = model.Amount, FixedIncomeId = model.Id, GeneratedForMonth = month, ReferenceMonth = month }; await SaveAsync(target, ct); }
            targets[income.Id] = target;
            foreach (var expense in expenses.Where(x => x.IncomeId == income.Id))
            {
                expense.IncomeId = target.Id;
                var fixedExpense = expense.FixedExpenseId.HasValue ? await db.FixedExpenses.FindAsync([expense.FixedExpenseId.Value], ct) : null;
                if (fixedExpense is null) { fixedExpense = new() { ClientId=FinanceIdentity.ForSource("FixedExpense",expense.ClientId), FixedIncomeId = model.Id, Description = expense.Description, Category = expense.Category, Amount = expense.Amount, DueDay = expense.DueDate.Day, Active = model.Active }; await SaveAsync(fixedExpense, ct); ec++; } else er++;
                if (await db.Expenses.AnyAsync(x => x.FixedExpenseId == fixedExpense.Id && x.GeneratedForMonth == month && x.Id != expense.Id, ct)) throw new ArgumentException("Já existe uma despesa deste modelo na competência.");
                expense.FixedExpenseId = fixedExpense.Id; expense.GeneratedForMonth = month; expense.Replicable = false;
            }
        }
        await db.SaveChangesAsync(ct);
        foreach (var item in targets.Values.Select(x => (Type: "INCOME", Model: x.FixedIncomeId, Month: month)).Concat(incomes.Select(x => (Type: "INCOME", Model: x.FixedIncomeId, Month: Month(x.ReferenceMonth)))).Concat(expenses.Select(x => (Type: "EXPENSE", Model: x.FixedExpenseId, Month: month))).Distinct())
            if (item.Model.HasValue && !await db.MonthlyPreparationItems.AnyAsync(x => x.ModelType == item.Type && x.ModelId == item.Model && x.ReferenceMonth == item.Month, ct)) db.MonthlyPreparationItems.Add(new() { ModelType = item.Type, ModelId = item.Model.Value, ReferenceMonth = item.Month, State = "GENERATED" });
        await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return new(ic, ir, ec, er);
    }

    internal static DateOnly Month(DateOnly d) => new(d.Year, d.Month, 1);
    private async Task<Guid> SnapshotIdentityAsync(string type,Guid model,DateOnly month,CancellationToken ct)
    {
        var id=FinanceIdentity.ForMonth(type,model,month);
        return await db.SyncLogs.AnyAsync(x=>x.EntityType==type&&x.ClientId==id&&x.Deleted,ct)?Guid.NewGuid():id;
    }
    private static void Validate(FinancialRecord record)
    {
        var amount = record switch { Income x => x.Amount, Expense x => x.Amount, Payment x => x.Amount, FixedIncome x => x.Amount, FixedExpense x => x.Amount, _ => 1m };
        if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new ArgumentException("Informe um valor maior que zero, com até duas casas decimais.");
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(record, new(record), results, true)) throw new ArgumentException(string.Join(" ", results.Select(x => x.ErrorMessage)));
    }
}
