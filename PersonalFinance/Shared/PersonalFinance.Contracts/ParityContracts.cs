using System.Text.Json;

namespace PersonalFinance.Contracts;

public sealed class BalanceInsights
{
    public int Accounts { get; set; }
    public int Settled { get; set; }
    public int Partial { get; set; }
    public int Unpaid { get; set; }
    public int Overdue { get; set; }
    public decimal OverdueAmount { get; set; }
    public decimal? IncomeCommitment { get; set; }
    public decimal? RecurringShare { get; set; }
    public decimal PersonalBudget { get; set; }
    public decimal PersonalSpent { get; set; }
    public decimal PersonalDailyAverage { get; set; }
    public decimal? PersonalDailyLimit { get; set; }
    public decimal? PersonalProjection { get; set; }
    public int DaysWithPersonalSpending { get; set; }
    public int DaysElapsed { get; set; }
    public int DaysRemaining { get; set; }
    public int PaymentCount { get; set; }
    public decimal AveragePayment { get; set; }
    public decimal LargestPayment { get; set; }
    public string LargestPaymentDescription { get; set; } = "";
    public int DaysWithOutflows { get; set; }
    public string LargestCategory { get; set; } = "";
    public decimal LargestCategoryAmount { get; set; }
    public decimal LargestCategoryShare { get; set; }
    public decimal TopThreeShare { get; set; }
    public IReadOnlyList<CategorySettlement> CategorySettlements { get; set; } = [];
}
public sealed record CategorySettlement(string Category, decimal Planned, decimal Paid);
public sealed record PaymentListItem(Payment Payment, string Expense, string Category, string Payer, DateOnly DueDate);
public sealed record PreparationResult(int IncomesCreated, int IncomesExisting, int ExpensesCreated, int ExpensesExisting, int Ignored);
public sealed record ConvertFixedRequest(DateOnly Month, IReadOnlyList<int> IncomeIds, IReadOnlyList<int> ExpenseIds);
public sealed record ConvertFixedResult(int IncomesCreated, int IncomesReused, int ExpensesCreated, int ExpensesReused);
public sealed record SettleRequest(DateOnly Date, string Note, Guid OperationId);
public sealed record PersonalExpenseRequest(int ExpenseId, DateOnly Date, string Note, decimal Amount, Guid ClientId);

// Relations travel by UUID; database-local integer IDs never identify a remote parent.
public sealed record SyncChange(Guid OperationId, string EntityType, Guid ClientId, bool Deleted, JsonElement? Data,
    Guid? ParentClientId = null, Guid? FixedClientId = null)
{
    public long? ExpectedRevision { get; init; }
    public long? Revision { get; init; }
}
public sealed record SyncPushRequest(IReadOnlyList<SyncChange> Changes);
public sealed record SyncPushResult(IReadOnlyList<Guid> Accepted, string? Error = null)
{
    public IReadOnlyList<SyncConflict> Conflicts { get; init; } = [];
    public IReadOnlyDictionary<Guid,long> Revisions { get; init; } = new Dictionary<Guid,long>();
}
public sealed record SyncConflict(Guid OperationId, string EntityType, Guid ClientId, string Reason, SyncChange? Current);
public sealed record FinanceCommand(Guid OperationId, string Kind, string EntityType, Guid ClientId, long? ExpectedRevision, JsonElement? Data,
    Guid? ParentClientId = null, Guid? FixedClientId = null, Guid? DependsOn = null);
public sealed record FinanceCommandResult(string Status, JsonElement? Data = null, string? Error = null, SyncChange? Current = null)
{
    public IReadOnlyDictionary<Guid, long> Revisions { get; init; } = new Dictionary<Guid, long>();
}
public sealed record PrepareIdentityItem(string ModelType, Guid ModelClientId, bool Selected,
    string Payer, string Description, string Type, string Category, decimal Amount, int DueDay);
public sealed record PrepareIdentityRequest(DateOnly Month, IReadOnlyList<PrepareIdentityItem> Items);
public sealed record ConvertIdentityRequest(DateOnly Month, IReadOnlyList<Guid> IncomeClientIds, IReadOnlyList<Guid> ExpenseClientIds);
public sealed record SyncPullResult(long Cursor, IReadOnlyList<SyncChange> Changes, bool HasMore);
public sealed record SyncStatus(int Incomes, int Expenses, int Payments, int FixedIncomes, int FixedExpenses,
    decimal IncomeTotal, decimal ExpenseTotal, decimal PaymentTotal, long Cursor);
public sealed record BootstrapSyncRequest(Guid OperationId, IReadOnlyList<SyncChange> Changes, long ExpectedCursor,
    bool Reconciled = false);
public sealed record BootstrapCheckpoint(BootstrapSyncRequest Request, Dictionary<Guid, Guid> Mapping, string Server, string Email);
public sealed record DeviceSnapshotRequest(Guid OperationId, IReadOnlyList<SyncChange> Changes, long ExpectedCursor);
public sealed record DeviceSnapshotCheckpoint(DeviceSnapshotRequest Request, string Server, string Email,
    IReadOnlyList<Guid> PendingIds, IReadOnlyList<Guid> CommandIds);
