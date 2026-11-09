using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Contracts;
using System.Text.Json;

namespace PersonalFinance.Api.Data;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<FixedIncome> FixedIncomes => Set<FixedIncome>();
    public DbSet<FixedExpense> FixedExpenses => Set<FixedExpense>();
    public DbSet<MonthlyPreparationItem> MonthlyPreparationItems => Set<MonthlyPreparationItem>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
    public DbSet<SyncReceipt> SyncReceipts => Set<SyncReceipt>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    public override Task<int> SaveChangesAsync(CancellationToken ct = default) => Database.CurrentTransaction is not null
        ? SaveTrackedAsync(ct) : Database.CreateExecutionStrategy().ExecuteAsync(() => SaveTrackedAsync(ct));

    private async Task<int> SaveTrackedAsync(CancellationToken ct)
    {
        ChangeTracker.DetectChanges();
        var changes = ChangeTracker.Entries<FinancialRecord>().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        if (changes.Count == 0 && !ChangeTracker.Entries<SyncLog>().Any(x => x.State == EntityState.Added)) return await base.SaveChangesAsync(ct);
        // Keep the data and its incremental feed in the same transaction.
        var ownTransaction = Database.CurrentTransaction is null;
        var snapshots = ownTransaction ? ChangeTracker.Entries().Select(x => (x.Entity, x.State, Values: x.CurrentValues.Clone())).ToList() : null;
        await using var transaction = ownTransaction ? await Database.BeginTransactionAsync(ct) : null;
        try
        {
            // Revision allocation and commit order must agree; otherwise a reader can skip
            // a smaller, still-uncommitted revision after seeing a larger one.
            if (Database.IsSqlServer())
                await Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result=sp_getapplock @Resource=N'PersonalFinance.Sync',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=30000; IF @result<0 THROW 51000, 'Falha ao obter bloqueio de sincronização.', 1;", ct);
            var logs = new List<(FinancialRecord Entity, SyncLog Log)>();
            foreach (var entry in changes)
            {
                var e = entry.Entity;
                if (e.ClientId == Guid.Empty) e.ClientId = Guid.NewGuid();
                if (entry.State != EntityState.Deleted)
                {
                    e.UpdatedAtUtc = DateTime.UtcNow;
                    if (entry.State == EntityState.Added) e.CreatedAtUtc = e.UpdatedAtUtc;
                }
                var log = new SyncLog { EntityType = e.GetType().Name, ClientId = e.ClientId, Deleted = entry.State == EntityState.Deleted };
                logs.Add((e, log));
            }
            var count = await base.SaveChangesAsync(ct);
            foreach (var (e, log) in logs)
            {
                if (!log.Deleted)
                {
                    log.ParentClientId = e switch
                    {
                        Expense x => await Incomes.Where(p => p.Id == x.IncomeId).Select(p => (Guid?)p.ClientId).SingleOrDefaultAsync(ct),
                        Payment x => await Expenses.Where(p => p.Id == x.ExpenseId).Select(p => (Guid?)p.ClientId).SingleOrDefaultAsync(ct),
                        FixedExpense x => await FixedIncomes.Where(p => p.Id == x.FixedIncomeId).Select(p => (Guid?)p.ClientId).SingleOrDefaultAsync(ct),
                        MonthlyPreparationItem x when x.ModelType == "INCOME" => await FixedIncomes.Where(p => p.Id == x.ModelId).Select(p => (Guid?)p.ClientId).SingleOrDefaultAsync(ct),
                        MonthlyPreparationItem x => await FixedExpenses.Where(p => p.Id == x.ModelId).Select(p => (Guid?)p.ClientId).SingleOrDefaultAsync(ct),
                        _ => null
                    };
                    log.FixedClientId = e switch
                    {
                        Income x when x.FixedIncomeId.HasValue => await FixedIncomes.Where(p => p.Id == x.FixedIncomeId).Select(p => (Guid?)p.ClientId).SingleOrDefaultAsync(ct),
                        Expense x when x.FixedExpenseId.HasValue => await FixedExpenses.Where(p => p.Id == x.FixedExpenseId).Select(p => (Guid?)p.ClientId).SingleOrDefaultAsync(ct),
                        _ => null
                    };
                }
                SyncLogs.Add(log);
            }
            await base.SaveChangesAsync(ct);
            foreach (var (e, log) in logs)
                if (!log.Deleted)
                {
                    e.Revision = log.Id;
                    log.Payload = JsonSerializer.Serialize(e, e.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
            await base.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return count;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            if (snapshots is not null)
            {
                ChangeTracker.Clear();
                foreach (var snapshot in snapshots) { var entry = Entry(snapshot.Entity); entry.CurrentValues.SetValues(snapshot.Values); entry.State = snapshot.State; }
            }
            throw;
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<SyncLog>().HasIndex(x => new { x.EntityType, x.ClientId });
        builder.Entity<RefreshSession>().HasKey(x => x.TokenHash);
        builder.Entity<RefreshSession>().Property(x => x.TokenHash).HasMaxLength(64);
        builder.Entity<RefreshSession>().HasIndex(x => x.FamilyId);
        foreach (var entity in builder.Model.GetEntityTypes().Where(x => typeof(FinancialRecord).IsAssignableFrom(x.ClrType)))
        {
            builder.Entity(entity.ClrType).Property(nameof(FinancialRecord.ClientId)).IsRequired();
            builder.Entity(entity.ClrType).HasIndex(nameof(FinancialRecord.ClientId)).IsUnique();
        }
        builder.Entity<Income>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<Expense>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<Payment>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<FixedIncome>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<FixedExpense>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<Expense>().HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<FixedExpense>().HasIndex(x => new { x.FixedIncomeId, x.Description });
        builder.Entity<MonthlyPreparationItem>().HasIndex(x => new { x.ModelType, x.ModelId, x.ReferenceMonth }).IsUnique();
        builder.Entity<Income>().HasIndex(x => new { x.FixedIncomeId, x.GeneratedForMonth }).IsUnique().HasFilter("[FixedIncomeId] IS NOT NULL AND [GeneratedForMonth] IS NOT NULL");
        builder.Entity<Expense>().HasIndex(x => new { x.FixedExpenseId, x.GeneratedForMonth }).IsUnique().HasFilter("[FixedExpenseId] IS NOT NULL AND [GeneratedForMonth] IS NOT NULL");
    }
}
