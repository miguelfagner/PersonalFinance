using PersonalFinance.Contracts;
using PersonalFinance.Resources.ViewModels;
using AndroidModels = PersonalFinance.Resources.Models;

internal static class DashboardRegressionTests
{
    internal static void Run()
    {
        var month = new DateOnly(2026, 9, 1);
        var today = new DateOnly(2026, 9, 17);
        var expenses = Fixture(month);
        var incomes = new[] { new Income { ReferenceMonth = month, Amount = 3000 } };
        var summary = BalanceCalculator.Calculate(month, incomes, expenses, today);
        Check(summary.Expenses == 2080 && summary.Paid == 1200 && summary.Remaining == 880, "Partial and capped settlement per expense");
        Check(summary.Outflows == 2050 && summary.CashBalance == 950, "Cash flow includes payments of other due months");
        Check(summary.Categories.Single(x => x.Category == "ALIMENTAÇÃO").Amount == 680, "Case, accents and whitespace normalization");
        Check(summary.PersonalSpendingByWeek.Count == 5, "September 2026 has five Sunday weeks");
        Check(summary.PersonalSpendingByWeek.Select(x => x.Amount).SequenceEqual(new decimal[] { 100, 300, 0, 0, 0 }), "Sunday boundary, future payments and other due months excluded");
        Check(summary.PersonalSpendingByWeek[0].Start.Day == 1 && summary.PersonalSpendingByWeek[0].End.Day == 5 && summary.PersonalSpendingByWeek[1].Start.Day == 6, "Sunday week date ranges");

        // Compare with the actual Android calculator, including open balance and cash totals.
        var androidExpenses = expenses.Select(x => new AndroidModels.Despesa
        {
            Id = x.Id, Vencimento = x.DueDate.ToDateTime(TimeOnly.MinValue), Valor = x.Amount, Categoria = x.Category
        }).ToList();
        var androidPayments = expenses.SelectMany(x => x.Payments.Select(p => new AndroidModels.Transacao
        {
            Id = p.Id, DespesaId = x.Id, Data = p.Date.ToDateTime(TimeOnly.MinValue), Valor = p.Amount,
            Despesa = androidExpenses.Single(d => d.Id == x.Id)
        })).ToList();
        var open = androidExpenses.Select(x =>
        {
            var paid = androidPayments.Where(p => p.DespesaId == x.Id && p.Data.Date <= today.ToDateTime(TimeOnly.MinValue)).Sum(p => p.Valor);
            return new AndroidModels.DespesaEmAberto { Despesa = x, TotalPago = paid, ValorRestante = Math.Max(0, x.Valor - paid) };
        }).Where(x => x.ValorRestante > 0).ToList();
        var android = BalancoMensalCalculator.Calcular(
            incomes.Select(x => new AndroidModels.Receita { MesReferencia = x.ReferenceMonth.ToDateTime(TimeOnly.MinValue), Valor = x.Amount }),
            androidExpenses, androidPayments, open, 9, 2026, today.ToDateTime(TimeOnly.MinValue));
        Check(summary.Income == android.TotalReceita && summary.Expenses == android.TotalPlanejado &&
            summary.Paid == android.TotalQuitadoCompetencia && summary.Remaining == android.TotalEmAberto &&
            summary.Outflows == android.TotalSaidasMes && summary.Balance == android.SaldoPlanejado,
            "Android financial indicator parity");
        Check(summary.PersonalSpendingByWeek.Sum(x => x.Amount) == android.PessoalPagoNoMes, "Android personal chart parity");
        var future = BalanceCalculator.Calculate(month.AddMonths(1), [], Fixture(month.AddMonths(1)), today);
        Check(future.Outflows == 0 && future.PersonalSpendingByWeek.All(x => x.Payments == 0), "Future months have no realized payments");
        foreach (var (date, weeks) in new[] { (new DateOnly(2026, 2, 1), 4), (new DateOnly(2020, 8, 1), 6), (new DateOnly(2024, 2, 1), 5) })
        {
            var empty = BalanceCalculator.Calculate(date, [], [], today);
            Check(empty.PersonalSpendingByWeek.Count == weeks && empty.PersonalSpendingByWeek[^1].End == date.AddMonths(1).AddDays(-1), "Four/six weeks and leap-year coverage");
            Check(empty.Income == 0 && empty.Categories.Count == 0 && empty.Remaining == 0, "Empty months");
        }
        Console.WriteLine("PASS dashboard: Android parity, partial/overpayment, cross-month cash flow, Sunday weeks, future/empty/leap months");
    }

    internal static List<Expense> Fixture(DateOnly month)
    {
        Payment Pay(int id, int expenseId, int day, decimal amount) => new() { Id = id, ExpenseId = expenseId, Date = month.AddDays(day - 1), Amount = amount };
        return
        [
            new Expense { Id = 1, Description = "Orçamento pessoal", Category = " pessoal ", DueDate = month.AddDays(14), Amount = 1000,
                Payments = [Pay(1, 1, 5, 100), Pay(2, 1, 6, 300), Pay(3, 1, 25, 500)] },
            new Expense { Id = 2, Description = "Mercado", Category = "alimentacao", DueDate = month.AddDays(9), Amount = 600,
                Payments = [new Payment { Id = 4, ExpenseId = 2, Date = month.AddDays(-2), Amount = 200 }, Pay(5, 2, 10, 550)] },
            new Expense { Id = 3, Description = "Feira", Category = " ALIMENTAÇÃO ", DueDate = month.AddDays(8), Amount = 80 },
            new Expense { Id = 4, Description = "Transporte", Category = "Transporte", DueDate = month.AddDays(10), Amount = 300, Payments = [Pay(6, 4, 11, 200)] },
            new Expense { Id = 5, Description = "Livros", Category = "Educação", DueDate = month.AddDays(11), Amount = 60 },
            new Expense { Id = 6, Description = "Outros", Category = "", DueDate = month.AddDays(12), Amount = 40 },
            new Expense { Id = 7, Description = "Despesa pessoal anterior", Category = "PESSOAL", DueDate = month.AddDays(-1), Amount = 900, Payments = [Pay(7, 7, 12, 900)] }
        ];
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
