using PersonalFinance.Resources.Models;
using System.Globalization;
using System.Text;

namespace PersonalFinance.Resources.ViewModels
{
    internal enum PeriodoBalanco
    {
        Passado,
        Atual,
        Futuro
    }

    internal sealed class CategoriaBalanco
    {
        internal string Nome { get; init; } = string.Empty;
        internal decimal Planejado { get; init; }
        internal decimal Quitado { get; init; }
    }

    internal sealed class BalancoMensalResumo
    {
        internal PeriodoBalanco Periodo { get; init; }
        internal int DiasNoMes { get; init; }
        internal int DiasDecorridos { get; init; }
        internal int DiasRestantes { get; init; }

        internal decimal TotalReceita { get; init; }
        internal decimal TotalPlanejado { get; init; }
        internal decimal TotalQuitadoCompetencia { get; init; }
        internal decimal TotalSaidasMes { get; init; }
        internal decimal TotalEmAberto { get; init; }
        internal decimal SaldoPlanejado { get; init; }
        internal decimal SaldoFluxoMes { get; init; }
        internal decimal? ComprometimentoRenda { get; init; }
        internal decimal? PesoRecorrentes { get; init; }

        internal int TotalContas { get; init; }
        internal int ContasQuitadas { get; init; }
        internal int ContasParciais { get; init; }
        internal int ContasSemPagamento { get; init; }
        internal int ContasVencidas { get; init; }
        internal decimal ValorVencido { get; init; }

        internal decimal PessoalPlanejado { get; init; }
        internal decimal PessoalQuitadoCompetencia { get; init; }
        internal decimal PessoalPagoNoMes { get; init; }
        internal decimal MediaPessoalDiaria { get; init; }
        internal decimal? LimitePessoalDiario { get; init; }
        internal decimal? ProjecaoPessoal { get; init; }
        internal int DiasComGastoPessoal { get; init; }

        internal int QuantidadeTransacoes { get; init; }
        internal int DiasComSaidas { get; init; }
        internal decimal TicketMedio { get; init; }
        internal decimal MaiorLancamento { get; init; }
        internal string MaiorLancamentoDescricao { get; init; } = string.Empty;
        internal string MaiorCategoria { get; init; } = string.Empty;
        internal decimal MaiorCategoriaValor { get; init; }
        internal decimal ParticipacaoMaiorCategoria { get; init; }
        internal decimal ConcentracaoTopTres { get; init; }

        internal IReadOnlyList<CategoriaBalanco> Categorias { get; init; } = Array.Empty<CategoriaBalanco>();
    }

    internal static class BalancoMensalCalculator
    {
        internal static BalancoMensalResumo Calcular(
            IEnumerable<Receita> receitas, IEnumerable<Despesa> despesas, IEnumerable<Transacao> transacoes,
            IEnumerable<DespesaEmAberto> despesasEmAberto, int mes, int ano, DateTime hoje)
        {
            var localExpenses=despesas.ToList();
            var payments=transacoes.ToList();
            var all=localExpenses.Concat(payments.Where(x=>x.Despesa is not null).Select(x=>x.Despesa)).GroupBy(x=>x.Id).Select(x=>x.First()).ToList();
            var open=despesasEmAberto.GroupBy(x=>x.Despesa.Id).ToDictionary(x=>x.Key,x=>x.First());
            var competencePaid=localExpenses.ToDictionary(x=>x.Id,x=>x.Valor-(open.TryGetValue(x.Id,out var item)?Math.Clamp(item.ValorRestante,0,x.Valor):0));
            var shared=PersonalFinance.Contracts.BalanceCalculator.Calculate(new(ano,mes,1),
                receitas.Select(x=>new PersonalFinance.Contracts.Income{ReferenceMonth=DateOnly.FromDateTime(x.MesReferencia),Amount=x.Valor}),
                all.Select(x=>new PersonalFinance.Contracts.Expense{Id=x.Id,DueDate=DateOnly.FromDateTime(x.Vencimento),Description=x.Descricao,Category=x.Categoria,Amount=x.Valor,FixedExpenseId=x.DespesaFixaId,
                    Payments=payments.Where(p=>p.DespesaId==x.Id).Select(p=>new PersonalFinance.Contracts.Payment{Id=p.Id,ExpenseId=x.Id,Date=DateOnly.FromDateTime(p.Data),Amount=p.Valor,Note=p.Observacao}).ToList()}),
                DateOnly.FromDateTime(hoje),competencePaid);
            var i=shared.Insights;
            var current=new DateTime(hoje.Year,hoje.Month,1);var month=new DateTime(ano,mes,1);
            return new BalancoMensalResumo
            {
                Periodo=month<current?PeriodoBalanco.Passado:month>current?PeriodoBalanco.Futuro:PeriodoBalanco.Atual,
                DiasNoMes=DateTime.DaysInMonth(ano,mes),DiasDecorridos=i.DaysElapsed,DiasRestantes=i.DaysRemaining,
                TotalReceita=shared.Income,TotalPlanejado=shared.Expenses,TotalQuitadoCompetencia=shared.Paid,
                TotalSaidasMes=shared.Outflows,TotalEmAberto=shared.Remaining,SaldoPlanejado=shared.Balance,SaldoFluxoMes=shared.CashBalance,
                ComprometimentoRenda=i.IncomeCommitment,PesoRecorrentes=i.RecurringShare,
                TotalContas=i.Accounts,ContasQuitadas=i.Settled,ContasParciais=i.Partial,ContasSemPagamento=i.Unpaid,ContasVencidas=i.Overdue,ValorVencido=i.OverdueAmount,
                PessoalPlanejado=i.PersonalBudget,PessoalQuitadoCompetencia=i.CategorySettlements.Where(x=>x.Category=="PESSOAL").Sum(x=>x.Paid),
                PessoalPagoNoMes=i.PersonalSpent,MediaPessoalDiaria=i.PersonalDailyAverage,LimitePessoalDiario=i.PersonalDailyLimit,ProjecaoPessoal=i.PersonalProjection,
                DiasComGastoPessoal=i.DaysWithPersonalSpending,QuantidadeTransacoes=i.PaymentCount,DiasComSaidas=i.DaysWithOutflows,
                TicketMedio=i.AveragePayment,MaiorLancamento=i.LargestPayment,MaiorLancamentoDescricao=i.LargestPaymentDescription,
                MaiorCategoria=i.LargestCategory,MaiorCategoriaValor=i.LargestCategoryAmount,ParticipacaoMaiorCategoria=i.LargestCategoryShare,ConcentracaoTopTres=i.TopThreeShare,
                Categorias=i.CategorySettlements.Select(x=>new CategoriaBalanco{Nome=x.Category,Planejado=x.Planned,Quitado=x.Paid}).ToList()
            };
        }

        internal static string NormalizarCategoria(string? categoria)
        {
            if (string.IsNullOrWhiteSpace(categoria))
            {
                return "OUTROS";
            }

            var decomposta = categoria
                .Trim()
                .ToUpperInvariant()
                .Normalize(NormalizationForm.FormD);
            var semAcentos = new StringBuilder(decomposta.Length);

            foreach (var caractere in decomposta)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
                {
                    semAcentos.Append(caractere);
                }
            }

            var compactada = string.Join(
                " ",
                semAcentos
                    .ToString()
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

            return compactada switch
            {
                "ALIMENTACAO" => "ALIMENTAÇÃO",
                "CARTAO CREDITO" => "CARTÃO CRÉDITO",
                "COMBUSTIVEL" => "COMBUSTÍVEL",
                "FAMILIA" => "FAMÍLIA",
                "SAUDE" => "SAÚDE",
                _ => compactada
            };
        }

        private static bool CategoriaEh(string? categoria, string categoriaEsperada)
        {
            return string.Equals(
                NormalizarCategoria(categoria),
                categoriaEsperada,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string PrimeiraDescricaoValida(params string?[] descricoes)
        {
            return descricoes.FirstOrDefault(descricao => !string.IsNullOrWhiteSpace(descricao))?.Trim()
                ?? "SEM DESCRIÇÃO";
        }
    }
}
