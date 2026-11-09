using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Data;
using PersonalFinance.Api.Services;
using PersonalFinance.Contracts;

internal static class DeviceSnapshotRegressionTests
{
    internal static async Task RunAsync(FinanceDbContext db, string directory)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var service = new DeviceSnapshotService(db);
        var sync = new SyncService(db, new FinanceService(db));
        var before = await sync.StatusAsync(default);
        var oldIncome = await db.Incomes.AsNoTracking().FirstAsync();
        // Deliberately restore a previously deleted UUID, as the device is authoritative.
        var restored = new Income { ClientId = oldIncome.ClientId, ReferenceMonth = new(2026, 10, 1), Payer = "Celular", Amount = 5000 };
        db.SyncLogs.Add(new() { EntityType = "Income", ClientId = restored.ClientId, Deleted = true });
        await db.SaveChangesAsync();
        var fixedIncome = new FixedIncome { Payer = "Celular", Amount = 5000 };
        var fixedExpense = new FixedExpense { Amount = 600, Description = "Moradia", DueDay = 10 };
        var expense = new Expense { Amount = 1000, Description = "Moradia", CreatedOn = new(2026, 10, 1), DueDate = new(2026, 10, 10) };
        var payment = new Payment { Amount = 200, Date = new(2026, 10, 2), Note = "Parcial" };
        var decision = new MonthlyPreparationItem { ModelType = "EXPENSE", State = "GENERATED", ReferenceMonth = new(2026, 10, 1) };
        var retiredDecision = new MonthlyPreparationItem { ModelType = "EXPENSE", State = "IGNORED", ReferenceMonth = new(2026, 9, 1) };
        var retiredIncomeDecision = new MonthlyPreparationItem { ModelType = "INCOME", State = "GENERATED", ReferenceMonth = new(2026, 8, 1) };
        SyncChange Row(FinancialRecord row, Guid? parent = null, Guid? model = null) =>
            new(Guid.NewGuid(), row.GetType().Name, row.ClientId, false, JsonSerializer.SerializeToElement(row, row.GetType(), json), parent, model);
        // Out of order, with database-local foreign IDs absent: only UUID links identify parents.
        var rows = new[] { Row(payment, expense.ClientId), Row(expense, restored.ClientId, fixedExpense.ClientId),
            Row(decision, fixedExpense.ClientId), Row(retiredDecision), Row(retiredIncomeDecision, Guid.NewGuid()),
            Row(restored, model: fixedIncome.ClientId), Row(fixedExpense, fixedIncome.ClientId), Row(fixedIncome) }
            .Concat(Enumerable.Range(0, 600).Select(_ => Row(new Payment { Amount = 1, Date = payment.Date, Note = "Pagamento independente" }, expense.ClientId))).ToArray();
        var request = new DeviceSnapshotRequest(Guid.NewGuid(), rows, (await sync.StatusAsync(default)).Cursor);
        var result = await service.ApplyAsync(request, default);
        var status = await sync.StatusAsync(default);
        Check(status.Incomes == 1 && status.Expenses == 1 && status.Payments == 601 && status.FixedIncomes == 1 && status.FixedExpenses == 1,
            "Device replaces server history without duplicates");
        Check(status.IncomeTotal == 5000 && status.ExpenseTotal == 1000 && status.PaymentTotal == 800, "Device totals win");
        Check(result.Changes.Count == 606 && result.Changes.All(x => x.Revision > request.ExpectedCursor), "Large history beyond a feed page is acknowledged completely");
        using (var receipt = JsonDocument.Parse((await db.SyncReceipts.AsNoTracking().SingleAsync(x => x.Id == request.OperationId)).Result["device-snapshot:".Length..]))
        {
            var archive = receipt.RootElement.GetProperty("archivedDecisions").EnumerateArray().ToList();
            Check(archive.Count == 2 && archive.Any(x => x.GetProperty("clientId").GetGuid() == retiredDecision.ClientId) &&
                archive.Any(x => x.GetProperty("clientId").GetGuid() == retiredIncomeDecision.ClientId),
                "Decisions of deleted models are preserved in the receipt without blocking financial history");
        }
        var savedExpense = await db.Expenses.SingleAsync();
        Check(savedExpense.IncomeId == (await db.Incomes.SingleAsync()).Id && savedExpense.FixedExpenseId == (await db.FixedExpenses.SingleAsync()).Id &&
            (await db.Payments.FirstAsync()).ExpenseId == savedExpense.Id && (await db.MonthlyPreparationItems.SingleAsync()).ModelId == savedExpense.FixedExpenseId,
            "Device relationships and monthly decisions preserved");
        var previous = await service.BackupAsync(request.OperationId, default);
        Check(previous is not null, "Automatic backup committed with replacement");
        var backupPath = Path.Combine(directory, "before-device.db");
        await File.WriteAllBytesAsync(backupPath, previous!);
        await using (var backup = new SqliteConnection($"Data Source={backupPath};Pooling=False"))
        {
            await backup.OpenAsync();
            using var query = backup.CreateCommand(); query.CommandText = "SELECT count(*) FROM Receita";
            Check(Convert.ToInt32(await query.ExecuteScalarAsync()) == before.Incomes, "Server backup contains previous history");
        }
        Check((await db.SyncLogs.Where(x => x.Id > request.ExpectedCursor && x.Deleted).CountAsync()) > 0, "Removed server records produce tombstones");
        var finance = new FinanceService(db);
        var changed = await db.Incomes.SingleAsync(); changed.Amount = 5100; await finance.SaveAsync(changed, default);
        var retry = await service.ApplyAsync(request, default);
        Check(retry.Cursor == result.Cursor && (await db.Incomes.SingleAsync()).Amount == 5100, "Lost acknowledgement retry preserves later Web edits");
        await Fails(() => service.ApplyAsync(request with { Changes = rows.Take(1).ToList() }, default));
        await Fails(() => service.ApplyAsync(request with { OperationId = Guid.NewGuid() }, default));
        await Fails(async () => await service.ApplyAsync(new(Guid.NewGuid(), [], (await sync.StatusAsync(default)).Cursor), default));

        var stable = await sync.StatusAsync(default);
        var orphan = Row(new Payment { Amount = 1, Date = new(2026, 10, 1) }, Guid.NewGuid());
        var missingMessage = await Fails(() => service.ApplyAsync(new(Guid.NewGuid(), [orphan], stable.Cursor), default));
        Check(missingMessage.Contains(orphan.ClientId.ToString()) && missingMessage.Contains("pagamento") && missingMessage.Contains("despesa"),
            "Missing financial link identifies the exact record and parent type");
        var invalidRows = rows.Select(x => x.ClientId == payment.ClientId ? x with
        { Data = JsonSerializer.SerializeToElement(new Payment { Amount = -1, Date = payment.Date }, json) } : x).ToList();
        await Fails(() => service.ApplyAsync(new(Guid.NewGuid(), invalidRows, stable.Cursor), default));
        Check((await sync.StatusAsync(default)) == stable, "Invalid snapshot rolls back all replacements and feed changes");
        Console.WriteLine("PASS device first load: authoritative snapshot, archived decisions of deleted models, precise link errors, UUID relationships, totals, backup, tombstones, retry, stale cursor, empty protection and full rollback");
    }

    private static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL " + name); }
    private static async Task<string> Fails(Func<Task> action)
    {
        try { await action(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return error.Message; }
        throw new Exception("Expected snapshot validation failure");
    }
}
