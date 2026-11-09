using System.Globalization;
using System.Text;

namespace PersonalFinance.Contracts;

public static class BalanceCalculator
{
    public static BalanceSummary Calculate(DateOnly month, IEnumerable<Income> incomes,
        IEnumerable<Expense> expenses, DateOnly today, IReadOnlyDictionary<int, decimal>? competencePaid = null)
    {
        var start = new DateOnly(month.Year, month.Month, 1);
        var end = start.AddMonths(1);
        var allExpenses = expenses.ToList();
        var planned = allExpenses.Where(x => x.DueDate >= start && x.DueDate < end).ToList();
        // Competence settlement uses payments up to today, even if paid in another month.
        var details = planned.Select(x =>
        {
            var paid = competencePaid is not null && competencePaid.TryGetValue(x.Id, out var paidOverride) ? paidOverride : x.Payments.Where(p => p.Date <= today).Sum(p => p.Amount);
            var settled = Math.Clamp(paid, 0, Math.Max(0, x.Amount));
            return new ChartExpense(x.Id, x.Description, NormalizeCategory(x.Category), x.DueDate,
                x.Amount, settled, Math.Max(0, x.Amount - settled));
        }).OrderBy(x => x.DueDate).ThenBy(x => x.Id).ToList();
        var payments = allExpenses.SelectMany(x => x.Payments
            .Where(p => p.Date >= start && p.Date < end && p.Date <= today)
            .Select(p => new ChartPayment(p.Id, x.Id, x.Description, NormalizeCategory(x.Category),
                p.Note, p.Date, p.Amount))).OrderByDescending(x => x.Date).ThenBy(x => x.Id).ToList();
        var categories = details.GroupBy(x => x.Category)
            .Select(g => new CategoryTotal(g.Key, g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount).ThenBy(x => x.Category).ToList();
        // Match Android: only personal expenses due in this month feed the weekly chart.
        var personalIds = details.Where(x => x.Category == "PESSOAL").Select(x => x.Id).ToHashSet();
        var personalPayments = payments.Where(x => personalIds.Contains(x.ExpenseId)).ToList();
        var offset = (int)start.DayOfWeek;
        var days = DateTime.DaysInMonth(start.Year, start.Month);
        var weeks = Enumerable.Range(1, (days + offset - 1) / 7 + 1).Select(week =>
        {
            var first = start.AddDays(Math.Max(0, (week - 1) * 7 - offset));
            var last = start.AddDays(Math.Min(days - 1, week * 7 - offset - 1));
            var entries = personalPayments.Where(p => p.Date >= first && p.Date <= last).ToList();
            return new WeeklyPersonalSpending(week, first, last, entries.Sum(p => p.Amount), entries.Count);
        }).ToList();
        var income = incomes.Where(x => x.ReferenceMonth >= start && x.ReferenceMonth < end).Sum(x => x.Amount);
        var commitments = details.Sum(x => x.Amount);
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var elapsed = start < currentMonth ? days : start == currentMonth ? today.Day : 0;
        var remainingDays = start < currentMonth ? 0 : start == currentMonth ? days - today.Day + 1 : days;
        var personalBudget = details.Where(x => personalIds.Contains(x.Id)).Sum(x => x.Amount);
        var personalPaid = details.Where(x => personalIds.Contains(x.Id)).Sum(x => x.Paid);
        var personalSpent = personalPayments.Sum(x => x.Amount);
        var overdue = details.Where(x => x.Remaining > 0 && start <= currentMonth && (start < currentMonth || x.DueDate < today)).ToList();
        var largest = payments.OrderByDescending(x => x.Amount).FirstOrDefault();
        var outflowCategories = payments.GroupBy(x => x.Category).Select(x => new CategoryTotal(x.Key, x.Sum(p => p.Amount))).OrderByDescending(x => x.Amount).ToList();
        var outflows = payments.Sum(x => x.Amount);
        return new BalanceSummary(start, income, commitments, details.Sum(x => x.Paid),
            details.Sum(x => x.Remaining), income - commitments, categories)
        {
            Outflows = payments.Sum(x => x.Amount),
            PersonalSpendingByWeek = weeks,
            ExpenseDetails = details,
            PaymentDetails = payments,
            Insights = new BalanceInsights
            {
                Accounts = details.Count, Settled = details.Count(x => x.Remaining == 0),
                Partial = details.Count(x => x.Remaining > 0 && x.Paid > 0), Unpaid = details.Count(x => x.Remaining > 0 && x.Paid == 0),
                Overdue = overdue.Count, OverdueAmount = overdue.Sum(x => x.Remaining),
                IncomeCommitment = income > 0 ? commitments / income * 100 : null,
                RecurringShare = commitments > 0 ? planned.Where(x => x.FixedExpenseId.HasValue).Sum(x => x.Amount) / commitments * 100 : null,
                PersonalBudget = personalBudget, PersonalSpent = personalSpent,
                PersonalDailyAverage = elapsed > 0 ? personalSpent / elapsed : 0,
                PersonalDailyLimit = personalBudget > 0 && remainingDays > 0 ? Math.Max(0, personalBudget - personalPaid) / remainingDays : null,
                PersonalProjection = start == currentMonth && elapsed > 0 ? personalSpent / elapsed * days : null,
                DaysWithPersonalSpending = personalPayments.Select(x => x.Date).Distinct().Count(),
                DaysElapsed = elapsed, DaysRemaining = remainingDays, PaymentCount = payments.Count,
                AveragePayment = payments.Count > 0 ? payments.Sum(x => x.Amount) / payments.Count : 0,
                LargestPayment = largest?.Amount ?? 0, LargestPaymentDescription = largest is null ? "" : string.IsNullOrWhiteSpace(largest.Note) ? largest.Description : largest.Note,
                DaysWithOutflows = payments.Select(x => x.Date).Distinct().Count(),
                LargestCategory = outflowCategories.FirstOrDefault()?.Category ?? "", LargestCategoryAmount = outflowCategories.FirstOrDefault()?.Amount ?? 0,
                LargestCategoryShare = outflows > 0 ? (outflowCategories.FirstOrDefault()?.Amount ?? 0) / outflows * 100 : 0,
                TopThreeShare = outflows > 0 ? outflowCategories.Take(3).Sum(x => x.Amount) / outflows * 100 : 0,
                CategorySettlements = details.GroupBy(x => x.Category).Select(x => new CategorySettlement(x.Key, x.Sum(e => e.Amount), x.Sum(e => e.Paid))).OrderByDescending(x => x.Planned).ToList()
            }
        };
    }

    public static string NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return "OUTROS";
        var decomposed = category.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var plain = new StringBuilder();
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                plain.Append(character);
        var compact = string.Join(" ", plain.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact switch
        {
            "ALIMENTACAO" => "ALIMENTAÇÃO", "CARTAO CREDITO" => "CARTÃO CRÉDITO",
            "COMBUSTIVEL" => "COMBUSTÍVEL", "FAMILIA" => "FAMÍLIA", "SAUDE" => "SAÚDE",
            _ => compact
        };
    }
}
