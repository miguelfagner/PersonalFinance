using System.ComponentModel.DataAnnotations;

namespace PersonalFinance.Contracts;

public sealed class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public sealed record LoginResponse(string Token, DateTime ExpiresAt)
{
    public string? RefreshToken { get; init; }
    public DateTime? RefreshExpiresAt { get; init; }
}

public sealed record RefreshRequest(string RefreshToken);

public abstract class FinancialRecord
{
    public int Id { get; set; }
    public Guid ClientId { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public long Revision { get; set; }
}

public sealed class Income : FinancialRecord
{
    public DateOnly ReferenceMonth { get; set; }
    [MaxLength(100)] public string Payer { get; set; } = string.Empty;
    [MaxLength(500)] public string Description { get; set; } = string.Empty;
    [MaxLength(100)] public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int? FixedIncomeId { get; set; }
    public DateOnly? GeneratedForMonth { get; set; }
}

public sealed class Expense : FinancialRecord
{
    public int IncomeId { get; set; }
    public Guid? IncomeClientId { get; set; }
    public DateOnly CreatedOn { get; set; }
    public DateOnly DueDate { get; set; }
    public int InstallmentNumber { get; set; }
    [MaxLength(500)] public string Description { get; set; } = string.Empty;
    [MaxLength(100)] public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool Replicable { get; set; }
    public int? FixedExpenseId { get; set; }
    public DateOnly? GeneratedForMonth { get; set; }
    public List<Payment> Payments { get; set; } = [];
}

public sealed class Payment : FinancialRecord
{
    public int ExpenseId { get; set; }
    public Guid? ExpenseClientId { get; set; }
    [MaxLength(500)] public string Note { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
}

public sealed class FixedIncome : FinancialRecord
{
    [MaxLength(100)] public string Payer { get; set; } = string.Empty;
    [MaxLength(500)] public string Description { get; set; } = string.Empty;
    [MaxLength(100)] public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class FixedExpense : FinancialRecord
{
    public int FixedIncomeId { get; set; }
    [MaxLength(500)] public string Description { get; set; } = string.Empty;
    [MaxLength(100)] public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    [Range(1, 31)] public int DueDay { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class MonthlyPreparationItem : FinancialRecord
{
    [MaxLength(20)] public string ModelType { get; set; } = string.Empty;
    public int ModelId { get; set; }
    public DateOnly ReferenceMonth { get; set; }
    [MaxLength(20)] public string State { get; set; } = string.Empty;
}

public sealed record ExpenseListItem(Expense Expense, decimal Paid, decimal Remaining, string Status);
public sealed record BalanceSummary(DateOnly Month, decimal Income, decimal Expenses, decimal Paid, decimal Remaining, decimal Balance, IReadOnlyList<CategoryTotal> Categories)
{
    public decimal Outflows { get; init; }
    public decimal CashBalance => Income - Outflows;
    public IReadOnlyList<WeeklyPersonalSpending> PersonalSpendingByWeek { get; init; } = [];
    public IReadOnlyList<ChartExpense> ExpenseDetails { get; init; } = [];
    public IReadOnlyList<ChartPayment> PaymentDetails { get; init; } = [];
    public BalanceInsights Insights { get; init; } = new();
}
public sealed record CategoryTotal(string Category, decimal Amount);
public sealed record WeeklyPersonalSpending(int Week, DateOnly Start, DateOnly End, decimal Amount, int Payments);
public sealed record ChartExpense(int Id, string Description, string Category, DateOnly DueDate, decimal Amount, decimal Paid, decimal Remaining);
public sealed record ChartPayment(int Id, int ExpenseId, string Description, string Category, string Note, DateOnly Date, decimal Amount);
public sealed class PreparationPreviewItem
{
    public string ModelType { get; set; } = "";
    public int ModelId { get; set; }
    public int? FixedIncomeId { get; set; }
    public string Payer { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public int DueDay { get; set; } = 1;
    public bool Selected { get; set; }
    public bool AlreadyCreated { get; set; }
    public bool Ignored { get; set; }
    public bool NewSincePreparation { get; set; }
}
public sealed record PrepareMonthRequest(DateOnly Month, IReadOnlyList<int> FixedIncomeIds, IReadOnlyList<int> FixedExpenseIds)
{
    public IReadOnlyList<PreparationPreviewItem>? Items { get; init; }
    public Guid OperationId { get; init; } = Guid.NewGuid();
}
public sealed record ImportResult(int Incomes, int Expenses, int Payments, int FixedIncomes, int FixedExpenses, decimal IncomeTotal, decimal ExpenseTotal, decimal PaymentTotal);
