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
            IEnumerable<Receita> receitas,
            IEnumerable<Despesa> despesas,
            IEnumerable<Transacao> transacoes,
            IEnumerable<DespesaEmAberto> despesasEmAberto,
            int mes,
            int ano,
            DateTime hoje)
        {
            var inicioMes = new DateTime(ano, mes, 1);
            var inicioMesAtual = new DateTime(hoje.Year, hoje.Month, 1);
            var diasNoMes = DateTime.DaysInMonth(ano, mes);
            var periodo = inicioMes < inicioMesAtual
                ? PeriodoBalanco.Passado
                : inicioMes > inicioMesAtual ? PeriodoBalanco.Futuro : PeriodoBalanco.Atual;

            var despesasMes = despesas
                .Where(d => d.Vencimento.Year == ano && d.Vencimento.Month == mes)
                .ToList();
            var transacoesDoMes = transacoes
                .Where(t => t.Data.Year == ano && t.Data.Month == mes)
                .ToList();
            var transacoesMes = periodo switch
            {
                PeriodoBalanco.Atual => transacoesDoMes.Where(t => t.Data.Date <= hoje.Date).ToList(),
                PeriodoBalanco.Futuro => new List<Transacao>(),
                _ => transacoesDoMes
            };
            var abertasMes = despesasEmAberto
                .Where(item => item.Despesa != null &&
                               item.Despesa.Vencimento.Year == ano &&
                               item.Despesa.Vencimento.Month == mes)
                .ToList();

            var abertasPorDespesa = abertasMes
                .GroupBy(item => item.Despesa.Id)
                .ToDictionary(grupo => grupo.Key, grupo => grupo.First());

            var pagamentosCompetencia = despesasMes
                .Select(despesa =>
                {
                    var restante = abertasPorDespesa.TryGetValue(despesa.Id, out var aberta)
                        ? Math.Clamp(aberta.ValorRestante, 0m, despesa.Valor)
                        : 0m;

                    return new
                    {
                        Despesa = despesa,
                        Planejado = despesa.Valor,
                        Quitado = Math.Clamp(despesa.Valor - restante, 0m, despesa.Valor)
                    };
                })
                .ToList();

            var categorias = pagamentosCompetencia
                .GroupBy(item => NormalizarCategoria(item.Despesa.Categoria))
                .Select(grupo => new CategoriaBalanco
                {
                    Nome = grupo.Key,
                    Planejado = grupo.Sum(item => item.Planejado),
                    Quitado = grupo.Sum(item => item.Quitado)
                })
                .OrderByDescending(item => item.Planejado)
                .ThenBy(item => item.Nome)
                .ToList();

            var totalReceita = receitas
                .Where(r => r.MesReferencia.Year == ano && r.MesReferencia.Month == mes)
                .Sum(r => r.Valor);
            var totalPlanejado = pagamentosCompetencia.Sum(item => item.Planejado);
            var totalQuitadoCompetencia = pagamentosCompetencia.Sum(item => item.Quitado);
            var totalSaidasMes = transacoesMes.Sum(t => t.Valor);
            var totalEmAberto = abertasMes.Sum(item => item.ValorRestante);
            var totalRecorrente = despesasMes
                .Where(d => d.DespesaFixaId.HasValue)
                .Sum(d => d.Valor);

            var contasParciais = abertasMes.Count(item => item.TotalPago > 0m);
            var contasSemPagamento = abertasMes.Count - contasParciais;
            var contasVencidas = periodo == PeriodoBalanco.Futuro
                ? new List<DespesaEmAberto>()
                : abertasMes
                    .Where(item => periodo == PeriodoBalanco.Passado || item.Despesa.Vencimento.Date < hoje.Date)
                    .ToList();

            var diasDecorridos = periodo switch
            {
                PeriodoBalanco.Passado => diasNoMes,
                PeriodoBalanco.Atual => hoje.Day,
                _ => 0
            };
            var diasRestantes = periodo switch
            {
                PeriodoBalanco.Passado => 0,
                PeriodoBalanco.Atual => diasNoMes - hoje.Day + 1,
                _ => diasNoMes
            };

            var pessoalPlanejado = categorias
                .Where(item => CategoriaEh(item.Nome, "PESSOAL"))
                .Sum(item => item.Planejado);
            var pessoalQuitadoCompetencia = categorias
                .Where(item => CategoriaEh(item.Nome, "PESSOAL"))
                .Sum(item => item.Quitado);
            var idsDespesasPessoais = despesasMes
                .Where(d => CategoriaEh(d.Categoria, "PESSOAL"))
                .Select(d => d.Id)
                .ToHashSet();
            var transacoesPessoais = transacoesMes
                .Where(t => idsDespesasPessoais.Contains(t.DespesaId))
                .ToList();
            var pessoalPagoNoMes = transacoesPessoais.Sum(t => t.Valor);
            var mediaPessoalDiaria = diasDecorridos > 0 ? pessoalPagoNoMes / diasDecorridos : 0m;
            decimal? limitePessoalDiario = pessoalPlanejado > 0m && diasRestantes > 0
                ? Math.Max(0m, pessoalPlanejado - pessoalQuitadoCompetencia) / diasRestantes
                : null;
            decimal? projecaoPessoal = periodo == PeriodoBalanco.Atual && diasDecorridos > 0
                ? mediaPessoalDiaria * diasNoMes
                : null;

            var categoriasSaidas = transacoesMes
                .GroupBy(t => NormalizarCategoria(t.Despesa?.Categoria))
                .Select(grupo => new
                {
                    Categoria = grupo.Key,
                    Total = grupo.Sum(t => t.Valor)
                })
                .OrderByDescending(item => item.Total)
                .ToList();
            var maiorCategoria = categoriasSaidas.FirstOrDefault();
            var maiorTransacao = transacoesMes.OrderByDescending(t => t.Valor).FirstOrDefault();
            var descricaoMaiorTransacao = maiorTransacao == null
                ? string.Empty
                : PrimeiraDescricaoValida(
                    maiorTransacao.Observacao,
                    maiorTransacao.Despesa?.Descricao,
                    NormalizarCategoria(maiorTransacao.Despesa?.Categoria));

            return new BalancoMensalResumo
            {
                Periodo = periodo,
                DiasNoMes = diasNoMes,
                DiasDecorridos = diasDecorridos,
                DiasRestantes = diasRestantes,
                TotalReceita = totalReceita,
                TotalPlanejado = totalPlanejado,
                TotalQuitadoCompetencia = totalQuitadoCompetencia,
                TotalSaidasMes = totalSaidasMes,
                TotalEmAberto = totalEmAberto,
                SaldoPlanejado = totalReceita - totalPlanejado,
                SaldoFluxoMes = totalReceita - totalSaidasMes,
                ComprometimentoRenda = totalReceita > 0m ? totalPlanejado / totalReceita * 100m : null,
                PesoRecorrentes = totalPlanejado > 0m ? totalRecorrente / totalPlanejado * 100m : null,
                TotalContas = despesasMes.Count,
                ContasQuitadas = Math.Max(0, despesasMes.Count - abertasMes.Count),
                ContasParciais = contasParciais,
                ContasSemPagamento = contasSemPagamento,
                ContasVencidas = contasVencidas.Count,
                ValorVencido = contasVencidas.Sum(item => item.ValorRestante),
                PessoalPlanejado = pessoalPlanejado,
                PessoalQuitadoCompetencia = pessoalQuitadoCompetencia,
                PessoalPagoNoMes = pessoalPagoNoMes,
                MediaPessoalDiaria = mediaPessoalDiaria,
                LimitePessoalDiario = limitePessoalDiario,
                ProjecaoPessoal = projecaoPessoal,
                DiasComGastoPessoal = transacoesPessoais.Select(t => t.Data.Date).Distinct().Count(),
                QuantidadeTransacoes = transacoesMes.Count,
                DiasComSaidas = transacoesMes.Select(t => t.Data.Date).Distinct().Count(),
                TicketMedio = transacoesMes.Count > 0 ? totalSaidasMes / transacoesMes.Count : 0m,
                MaiorLancamento = maiorTransacao?.Valor ?? 0m,
                MaiorLancamentoDescricao = descricaoMaiorTransacao,
                MaiorCategoria = maiorCategoria?.Categoria ?? string.Empty,
                MaiorCategoriaValor = maiorCategoria?.Total ?? 0m,
                ParticipacaoMaiorCategoria = maiorCategoria != null && totalSaidasMes > 0m
                    ? maiorCategoria.Total / totalSaidasMes * 100m
                    : 0m,
                ConcentracaoTopTres = totalSaidasMes > 0m
                    ? categoriasSaidas.Take(3).Sum(item => item.Total) / totalSaidasMes * 100m
                    : 0m,
                Categorias = categorias
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
