using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Api.Data;
using PersonalFinance.Api.Services;
using PersonalFinance.Resources.Models;
using SQLite;

DashboardRegressionTests.Run();
if (args.Contains("--calculations-only")) return;

if (args.Contains("--preview"))
{
    await DashboardPreview.RunAsync();
    return;
}

// Uses an isolated local SQL Server database. Never loads production configuration.
var runId = Guid.NewGuid().ToString("N");
var directory = Path.GetFullPath(Path.Combine("Tests", "PersonalFinance.ImportTests", "obj", "import-tests", runId));
Directory.CreateDirectory(directory);
var databaseName = "PersonalFinanceImportTests_" + runId;
var options = new DbContextOptionsBuilder<FinanceDbContext>()
    .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};AttachDbFilename={Path.Combine(directory, "test.mdf")};Trusted_Connection=True;TrustServerCertificate=True",
        sql => sql.EnableRetryOnFailure())
    .Options;
await using var db = new FinanceDbContext(options);
try
{
    await db.Database.MigrateAsync();
    foreach (var ticks in new[] { true, false })
    {
        var backup = CreateBackup(directory, ticks, orphan: false);
        await using var stream = File.OpenRead(backup);
        var importer = new SqliteImporter(db);
        var result = await importer.ImportAsync(new FormFile(stream, 0, stream.Length, "file", "financeiro.db"), default);
        Assert(result.Incomes == 2 && result.Expenses == 2 && result.Payments == 2, "All rows imported");
        Assert(result.FixedIncomes == 1 && result.FixedExpenses == 1, "Fixed models imported");
        Assert(result.IncomeTotal == 3250.20m && result.ExpenseTotal == 850.55m && result.PaymentTotal == 300.10m, "Totals preserved exactly");
        var incomes = await db.Incomes.AsNoTracking().OrderBy(x => x.ReferenceMonth).ToListAsync();
        var expense = await db.Expenses.AsNoTracking().SingleAsync(x => x.Description == "Teste mensal");
        var payment = await db.Payments.AsNoTracking().SingleAsync(x => x.Note == "Pagamento parcial");
        Assert(incomes[0].ReferenceMonth == new DateOnly(2026, 8, 1) && incomes[1].ReferenceMonth == new DateOnly(2026, 9, 1), "Month dates preserved");
        Assert(expense.IncomeId == incomes[1].Id && expense.FixedExpenseId.HasValue, "Expense relationships remapped");
        Assert(expense.DueDate == new DateOnly(2026, 9, 15) && expense.GeneratedForMonth == new DateOnly(2026, 9, 1), "Due and generated dates preserved");
        Assert(payment.ExpenseId == expense.Id && payment.Date == new DateOnly(2026, 9, 17), "Payment relationship and date preserved");
        var dashboard = await new FinanceService(db).BalanceAsync(new DateOnly(2026, 9, 1), default, new DateOnly(2026, 9, 30));
        Assert(dashboard.Expenses == 600.50m && dashboard.Paid == 100.05m && dashboard.Outflows == 100.05m,
            "Dashboard SQL query and payment totals");
        stream.Position = 0;
        await ExpectAsync<ImportConflictException>(() => importer.ImportAsync(new FormFile(stream, 0, stream.Length, "file", "financeiro.db"), default));
        Assert(await db.Payments.CountAsync() == 2, "Repeated import cannot duplicate rows");
        Console.WriteLine($"PASS Android {(ticks ? "ticks" : "text")} dates, relationships, totals, repeated import");
        await ClearAsync(db);
    }

    var orphanBackup = CreateBackup(directory, ticks: true, orphan: true);
    await using (var stream = File.OpenRead(orphanBackup))
    {
        await ExpectAsync<InvalidDataException>(() => new SqliteImporter(db).ImportAsync(new FormFile(stream, 0, stream.Length, "file", "orphan.db"), default));
    }
    db.ChangeTracker.Clear();
    Assert(!await db.Incomes.AnyAsync() && !await db.FixedIncomes.AnyAsync() && !await db.Payments.AnyAsync(), "Invalid relationship rolls back all rows");
    Console.WriteLine("PASS full rollback for orphan relationship");

    using (var bytes = new MemoryStream([1, 2, 3, 4]))
    {
        var result = await ImportEndpoint.HandleAsync(new FormFile(bytes, 0, bytes.Length, "file", "invalid.db"),
            new SqliteImporter(db), new DefaultHttpContext(), NullLogger<SqliteImporter>.Instance, default);
        Assert(((IStatusCodeHttpResult)result).StatusCode == 400, "Invalid backup returns 400, not 500");
    }
    Console.WriteLine("PASS invalid SQLite error response");

    db.FixedIncomes.Add(new PersonalFinance.Contracts.FixedIncome { Payer = "Test", Amount = 1 });
    await db.SaveChangesAsync();
    await using (var stream = File.OpenRead(CreateBackup(directory, ticks: true, orphan: false)))
    {
        var result = await ImportEndpoint.HandleAsync(new FormFile(stream, 0, stream.Length, "file", "financeiro.db"),
            new SqliteImporter(db), new DefaultHttpContext(), NullLogger<SqliteImporter>.Instance, default);
        Assert(((IStatusCodeHttpResult)result).StatusCode == 409, "Existing fixed model returns conflict");
    }
    Assert(await db.FixedIncomes.CountAsync() == 1, "Existing data preserved");
    Console.WriteLine("PASS existing destination data preserved, HTTP 409");
    await ClearAsync(db);

    var invalidDate = CreateBackup(directory, ticks: true, orphan: false);
    using (var source = new SQLiteConnection(invalidDate)) source.Execute("UPDATE Receita SET MesReferencia = ?", "invalid-date");
    await using (var stream = File.OpenRead(invalidDate))
        await ExpectAsync<InvalidDataException>(() => new SqliteImporter(db).ImportAsync(new FormFile(stream, 0, stream.Length, "file", "invalid-date.db"), default));
    db.ChangeTracker.Clear();
    Assert(!await db.FixedIncomes.AnyAsync(), "Date failure rolls back previous inserts");
    Console.WriteLine("PASS invalid date rollback");
    await ClearAsync(db);
    await using var parityDb = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
    await ParityRegressionTests.RunAsync(parityDb, directory);
    await DeviceSnapshotRegressionTests.RunAsync(parityDb, directory);
}
finally
{
    // Exact database name was generated above; no production connection is used.
    await db.Database.EnsureDeletedAsync();
    Console.WriteLine("Removed isolated test database " + databaseName);
}

static async Task ClearAsync(FinanceDbContext db)
{
    db.ChangeTracker.Clear();
    await db.Payments.ExecuteDeleteAsync();
    await db.Expenses.ExecuteDeleteAsync();
    await db.Incomes.ExecuteDeleteAsync();
    await db.FixedExpenses.ExecuteDeleteAsync();
    await db.FixedIncomes.ExecuteDeleteAsync();
    await db.MonthlyPreparationItems.ExecuteDeleteAsync();
    await db.SyncReceipts.ExecuteDeleteAsync();
    await db.SyncLogs.ExecuteDeleteAsync();
}

static async Task ExpectAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static string CreateBackup(string directory, bool ticks, bool orphan)
{
    var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".db");
    using var source = new SQLiteConnection(path, ticks);
    source.CreateTable<Receita>(); source.CreateTable<Despesa>(); source.CreateTable<Transacao>();
    source.CreateTable<ReceitaFixa>(); source.CreateTable<DespesaFixa>();
    var fixedIncome = new ReceitaFixa { FontePagadora = "Empresa", Descricao = "Salário", Valor = 2000m };
    source.Insert(fixedIncome);
    var previousIncome = new Receita { MesReferencia = new DateTime(2026, 8, 1), FontePagadora = "Empresa", Valor = 1250.20m };
    source.Insert(previousIncome);
    var income = new Receita { MesReferencia = new DateTime(2026, 9, 1), FontePagadora = "Empresa", Valor = 2000m, ReceitaFixaId = fixedIncome.Id, CompetenciaGeracao = new DateTime(2026, 9, 1) };
    source.Insert(income);
    var fixedExpense = new DespesaFixa { ReceitaFixaId = fixedIncome.Id, Descricao = "Teste fixo", Valor = 600.50m, DiaVencimento = 15 };
    source.Insert(fixedExpense);
    var expense = new Despesa { ReceitaId = orphan ? 999 : income.Id, Descricao = "Teste mensal", Categoria = "CASA", Valor = 600.50m, DataCadastro = new DateTime(2026, 8, 28), Vencimento = new DateTime(2026, 9, 15), DespesaFixaId = fixedExpense.Id, CompetenciaGeracao = new DateTime(2026, 9, 1) };
    source.Insert(expense);
    var previousExpense = new Despesa { ReceitaId = previousIncome.Id, Descricao = "Despesa anterior", Valor = 250.05m, DataCadastro = new DateTime(2026, 8, 1), Vencimento = new DateTime(2026, 8, 20) };
    source.Insert(previousExpense);
    source.Insert(new Transacao { DespesaId = expense.Id, Observacao = "Pagamento parcial", Valor = 100.05m, Data = new DateTime(2026, 9, 17) });
    source.Insert(new Transacao { DespesaId = previousExpense.Id, Observacao = "Pagamento anterior", Valor = 200.05m, Data = new DateTime(2026, 8, 20) });
    return path;
}
