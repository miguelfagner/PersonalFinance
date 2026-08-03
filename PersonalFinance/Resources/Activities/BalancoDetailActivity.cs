using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Content.Res;
using System.Globalization;
using MikePhil.Charting.Animation;
using MikePhil.Charting.Charts;
using MikePhil.Charting.Components;
using MikePhil.Charting.Data;
using MikePhil.Charting.Formatter;
using MikePhil.Charting.Highlight;
using MikePhil.Charting.Listener;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using PersonalFinance.Resources.ViewModels;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Balanço Mensal")]
    public class BalancoDetailActivity : Activity
    {
        private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");
        private const string EstadoAnoReferencia = "balanco_ano_referencia";
        private const string EstadoMesReferencia = "balanco_mes_referencia";

        private TextView tvTotalReceita = null!;
        private TextView tvTotalDespesa = null!;
        private TextView tvSaldo = null!;
        private TextView tvTotalQuitado = null!;
        private TextView tvFaltaQuitar = null!;
        private TextView tvResumo = null!;
        private TextView tvSaldoPlanejado = null!;
        private TextView tvSaldoRealizado = null!;
        private TextView tvDesvioOrcamento = null!;
        private TextView tvComprometimentoRenda = null!;
        private TextView tvContasPendentes = null!;
        private TextView tvMediaDiaria = null!;
        private TextView tvLimiteDiario = null!;
        private TextView tvMaiorCategoria = null!;
        private TextView tvMesAtual = null!;
        private TextView tvProjecaoPessoal = null!;
        private TextView tvRitmoPessoal = null!;
        private TextView tvDiasComGastoPessoal = null!;
        private TextView tvDetalhePendencias = null!;
        private TextView tvContasVencidas = null!;
        private TextView tvPesoRecorrentes = null!;
        private TextView tvConcentracaoCategorias = null!;
        private TextView tvTicketMedio = null!;
        private TextView tvMaiorLancamento = null!;
        private TextView tvDiasComSaidas = null!;
        private TextView btnMesAnterior = null!;
        private TextView btnProximoMes = null!;
        private TextView tvErroCarregamento = null!;
        private Button btnVerTransacoesMes = null!;
        private Button btnVerGastosPessoais = null!;
        private LinearLayout layoutCategorias = null!;
        private ScrollView scrollIndicadores = null!;
        private ProgressBar progDespesa = null!;
        private ProgressBar progQuitacao = null!;
        private PieChart pieChartDespesas = null!;
        private BarChart barChartSemanal = null!;

        private DatabaseService _db = null!;
        private List<Despesa> _despesas = new();
        private GestureDetector _gestureDetector = null!;
        private LinearLayout _rootLayout = null!;
        private DateTime _mesReferencia = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime _mesExibido = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private int[] _idsDespesasPessoais = Array.Empty<int>();
        private int _versaoCarregamento;
        private bool _primeiroResume = true;
        private bool _destruida;

        protected override async void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_balanco);

            if (savedInstanceState != null &&
                savedInstanceState.ContainsKey(EstadoAnoReferencia) &&
                savedInstanceState.ContainsKey(EstadoMesReferencia))
            {
                var anoSalvo = savedInstanceState.GetInt(EstadoAnoReferencia);
                var mesSalvo = savedInstanceState.GetInt(EstadoMesReferencia);
                if (anoSalvo >= DateTime.MinValue.Year && anoSalvo <= DateTime.MaxValue.Year &&
                    mesSalvo is >= 1 and <= 12)
                {
                    _mesReferencia = new DateTime(anoSalvo, mesSalvo, 1);
                }
            }

            tvMesAtual = FindViewById<TextView>(Resource.Id.tvMesAtual);
            btnMesAnterior = FindViewById<TextView>(Resource.Id.btnMesAnterior);
            btnProximoMes = FindViewById<TextView>(Resource.Id.btnProximoMes);
            tvTotalReceita = FindViewById<TextView>(Resource.Id.tvTotalReceita);
            tvTotalDespesa = FindViewById<TextView>(Resource.Id.tvTotalDespesa);
            tvTotalQuitado = FindViewById<TextView>(Resource.Id.tvTotalQuitado);
            tvSaldo = FindViewById<TextView>(Resource.Id.tvSaldo);
            tvFaltaQuitar = FindViewById<TextView>(Resource.Id.tvFaltaQuitar);
            tvResumo = FindViewById<TextView>(Resource.Id.tvResumo);
            tvSaldoPlanejado = FindViewById<TextView>(Resource.Id.tvSaldoPlanejado);
            tvSaldoRealizado = FindViewById<TextView>(Resource.Id.tvSaldoRealizado);
            tvDesvioOrcamento = FindViewById<TextView>(Resource.Id.tvDesvioOrcamento);
            tvComprometimentoRenda = FindViewById<TextView>(Resource.Id.tvComprometimentoRenda);
            tvContasPendentes = FindViewById<TextView>(Resource.Id.tvContasPendentes);
            tvMediaDiaria = FindViewById<TextView>(Resource.Id.tvMediaDiaria);
            tvLimiteDiario = FindViewById<TextView>(Resource.Id.tvLimiteDiario);
            tvMaiorCategoria = FindViewById<TextView>(Resource.Id.tvMaiorCategoria);
            tvProjecaoPessoal = FindViewById<TextView>(Resource.Id.tvProjecaoPessoal);
            tvRitmoPessoal = FindViewById<TextView>(Resource.Id.tvRitmoPessoal);
            tvDiasComGastoPessoal = FindViewById<TextView>(Resource.Id.tvDiasComGastoPessoal);
            tvDetalhePendencias = FindViewById<TextView>(Resource.Id.tvDetalhePendencias);
            tvContasVencidas = FindViewById<TextView>(Resource.Id.tvContasVencidas);
            tvPesoRecorrentes = FindViewById<TextView>(Resource.Id.tvPesoRecorrentes);
            tvConcentracaoCategorias = FindViewById<TextView>(Resource.Id.tvConcentracaoCategorias);
            tvTicketMedio = FindViewById<TextView>(Resource.Id.tvTicketMedio);
            tvMaiorLancamento = FindViewById<TextView>(Resource.Id.tvMaiorLancamento);
            tvDiasComSaidas = FindViewById<TextView>(Resource.Id.tvDiasComSaidas);
            tvErroCarregamento = FindViewById<TextView>(Resource.Id.tvErroCarregamento);
            btnVerTransacoesMes = FindViewById<Button>(Resource.Id.btnVerTransacoesMes);
            btnVerGastosPessoais = FindViewById<Button>(Resource.Id.btnVerGastosPessoais);
            layoutCategorias = FindViewById<LinearLayout>(Resource.Id.layoutCategorias);
            scrollIndicadores = FindViewById<ScrollView>(Resource.Id.scrollIndicadores);

            progDespesa = FindViewById<ProgressBar>(Resource.Id.progDespesa);
            progQuitacao = FindViewById<ProgressBar>(Resource.Id.progQuitacao);

            pieChartDespesas = FindViewById<PieChart>(Resource.Id.pieChartDespesas);
            barChartSemanal = FindViewById<BarChart>(Resource.Id.barChartSemanal);

            try
            {
                _db = new DatabaseService();
            }
            catch (Exception)
            {
                MostrarErroCarregamento(_mesReferencia);
                btnMesAnterior.Enabled = false;
                btnProximoMes.Enabled = false;
                return;
            }
            _gestureDetector = new GestureDetector(this, new SwipeGestureListener(
                onSwipeRight: () => NavegarMesAsync(-1),
                onSwipeLeft: () => NavegarMesAsync(1)
            ));

            _rootLayout = FindViewById<LinearLayout>(Resource.Id.rootLayout);
            _rootLayout.Touch += (_, e) =>
            {
                if (e.Event != null)
                {
                    _gestureDetector.OnTouchEvent(e.Event);
                }
            };

            btnMesAnterior.Click += async (_, _) => await NavegarMesAsync(-1);
            btnProximoMes.Click += async (_, _) => await NavegarMesAsync(1);
            btnVerTransacoesMes.Click += (_, _) => AbrirTransacoesDoMes(apenasPessoais: false);
            btnVerGastosPessoais.Click += (_, _) => AbrirTransacoesDoMes(apenasPessoais: true);

            await CarregarDadosAsync(_mesReferencia, _mesReferencia.AddMonths(1));
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            outState.PutInt(EstadoAnoReferencia, _mesReferencia.Year);
            outState.PutInt(EstadoMesReferencia, _mesReferencia.Month);
            base.OnSaveInstanceState(outState);
        }

        protected override void OnDestroy()
        {
            _destruida = true;
            _versaoCarregamento++;
            base.OnDestroy();
        }

        protected override async void OnResume()
        {
            base.OnResume();

            if (_primeiroResume)
            {
                _primeiroResume = false;
                return;
            }

            await CarregarDadosAsync(_mesReferencia, _mesReferencia.AddMonths(1));
        }

        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e != null)
            {
                _gestureDetector?.OnTouchEvent(e);
            }

            return base.OnTouchEvent(e);
        }


        private async Task NavegarMesAsync(int deslocamento)
        {
            _mesReferencia = _mesReferencia.AddMonths(deslocamento);
            await CarregarDadosAsync(_mesReferencia, _mesReferencia.AddMonths(1));
        }

        private async Task CarregarDadosAsync(DateTime dtInicio, DateTime dtFinal)
        {
            var versao = ++_versaoCarregamento;
            try
            {
                var despesasTask = _db.ListaDespesasAsync(dtInicio, dtFinal);
                var receitasTask = _db.ListaReceitasAsync(dtInicio, dtFinal);
                var transacoesTask = _db.ListaTransacoesAsync(dtInicio, dtFinal);
                var despesasEmAbertoTask = _db.ListaDespesasEmAbertoAsync(
                    dtInicio,
                    dtFinal,
                    DateTime.Today.AddDays(1));

                await Task.WhenAll(despesasTask, receitasTask, transacoesTask, despesasEmAbertoTask);

                if (versao != _versaoCarregamento || _destruida || IsFinishing || IsDestroyed)
                {
                    return;
                }

                scrollIndicadores.Visibility = ViewStates.Visible;
                tvErroCarregamento.Visibility = ViewStates.Gone;
                _mesExibido = dtInicio;

                var despesas = await despesasTask ?? new List<Despesa>();
                var receitas = await receitasTask ?? new List<Receita>();
                var transacoes = await transacoesTask ?? new List<Transacao>();
                var despesasEmAberto = await despesasEmAbertoTask ?? new List<DespesaEmAberto>();

                _despesas = despesas;
                var mes = dtInicio.Month;
                var ano = dtInicio.Year;

                tvMesAtual.Text = dtInicio
                    .ToString("MMMM 'de' yyyy", CulturaPtBr)
                    .ToUpper(CulturaPtBr);

                var resumo = BalancoMensalCalculator.Calcular(
                    receitas,
                    despesas,
                    transacoes,
                    despesasEmAberto,
                    mes,
                    ano,
                    DateTime.Today);

                AtualizarIndicadores(resumo);
                AtualizarCategorias(resumo.Categorias);
                ConfigurarPieChartDespesas(this, pieChartDespesas, _despesas, mes, ano);

                var inicioMesAtual = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var transacoesRealizadas = dtInicio > inicioMesAtual
                    ? new List<Transacao>()
                    : dtInicio == inicioMesAtual
                        ? transacoes.Where(t => t.Data.Date <= DateTime.Today).ToList()
                        : transacoes;
                var idsDespesasPessoais = despesas
                    .Where(d => CategoriaEh(d.Categoria, "PESSOAL"))
                    .Select(d => d.Id)
                    .ToArray();
                _idsDespesasPessoais = idsDespesasPessoais;
                ConfigurarBarChartSemanalPorTransacoes(
                    barChartSemanal,
                    transacoesRealizadas,
                    mes,
                    ano,
                    idsDespesasPessoais);
            }
            catch (Exception)
            {
                if (versao == _versaoCarregamento && !_destruida && !IsFinishing && !IsDestroyed)
                {
                    MostrarErroCarregamento(dtInicio);
                    Toast.MakeText(this, "Não foi possível atualizar o balanço.", ToastLength.Short)?.Show();
                }
            }
        }

        private void MostrarErroCarregamento(DateTime mesReferencia)
        {
            tvMesAtual.Text = mesReferencia
                .ToString("MMMM 'de' yyyy", CulturaPtBr)
                .ToUpper(CulturaPtBr);
            scrollIndicadores.Visibility = ViewStates.Gone;
            tvErroCarregamento.Visibility = ViewStates.Visible;
        }

        private void AbrirTransacoesDoMes(bool apenasPessoais)
        {
            var intent = new Intent(this, typeof(TransacaoListActivity));
            intent.PutExtra("Mes", _mesExibido.Month);
            intent.PutExtra("Ano", _mesExibido.Year);

            if (apenasPessoais)
            {
                intent.PutExtra("Categoria", "PESSOAL");
                intent.PutExtra("DespesaIds", _idsDespesasPessoais);
            }

            StartActivity(intent);
        }

        private void AtualizarIndicadores(BalancoMensalResumo resumo)
        {
            var percentualQuitado = PercentualDecimal(resumo.TotalQuitadoCompetencia, resumo.TotalPlanejado);
            var percentualContasQuitadas = PercentualDecimal(resumo.ContasQuitadas, resumo.TotalContas);

            tvSaldo.Text = FormatarMoeda(resumo.SaldoPlanejado);
            tvTotalReceita.Text = $"RECEITA CADASTRADA\n{FormatarMoeda(resumo.TotalReceita)}";
            tvTotalDespesa.Text = $"COMPROMISSOS\n{FormatarMoeda(resumo.TotalPlanejado)}";
            tvDesvioOrcamento.Text = resumo.TotalPlanejado > 0m
                ? $"QUITADO DA COMPETÊNCIA: {FormatarMoeda(resumo.TotalQuitadoCompetencia)} DE {FormatarMoeda(resumo.TotalPlanejado)} · {percentualQuitado:N0}%"
                : "NENHUM COMPROMISSO PLANEJADO";

            tvTotalQuitado.Text = $"SAÍDAS NO MÊS\n{FormatarMoeda(resumo.TotalSaidasMes)}";
            tvFaltaQuitar.Text = $"EM ABERTO AGORA\n{FormatarMoeda(resumo.TotalEmAberto)}";
            tvSaldoPlanejado.Text = $"SALDO APÓS COMPROMISSOS: {FormatarMoeda(resumo.SaldoPlanejado)}";
            tvSaldoRealizado.Text = $"RECEITA − SAÍDAS DO MÊS: {FormatarMoeda(resumo.SaldoFluxoMes)}";
            tvComprometimentoRenda.Text = resumo.ComprometimentoRenda.HasValue
                ? $"COMPROMETIMENTO DA RENDA: {resumo.ComprometimentoRenda.Value:N1}%"
                : "COMPROMETIMENTO DA RENDA: SEM RECEITA CADASTRADA";
            tvPesoRecorrentes.Text = resumo.PesoRecorrentes.HasValue
                ? $"PESO DOS COMPROMISSOS RECORRENTES: {resumo.PesoRecorrentes.Value:N1}%"
                : "PESO DOS COMPROMISSOS RECORRENTES: SEM DESPESAS";

            tvMediaDiaria.Text = $"MÉDIA PESSOAL/DIA\n{FormatarMoeda(resumo.MediaPessoalDiaria)}";
            tvLimiteDiario.Text = resumo.LimitePessoalDiario.HasValue
                ? $"DISPONÍVEL/DIA\n{FormatarMoeda(resumo.LimitePessoalDiario.Value)}"
                : resumo.PessoalPlanejado <= 0m
                    ? "DISPONÍVEL/DIA\nSEM ORÇAMENTO"
                    : "DISPONÍVEL/DIA\nMÊS ENCERRADO";
            tvProjecaoPessoal.Text = FormatarProjecaoPessoal(resumo);
            tvRitmoPessoal.Text = FormatarRitmoPessoal(resumo);
            var ritmoAcimaDoOrcamento = resumo.ProjecaoPessoal.HasValue &&
                                        resumo.PessoalPlanejado > 0m &&
                                        resumo.ProjecaoPessoal.Value > resumo.PessoalPlanejado;
            tvRitmoPessoal.SetTextColor(Color.ParseColor(
                resumo.PessoalPlanejado <= 0m
                    ? "#667785"
                    : ritmoAcimaDoOrcamento ? "#A12D3D" : "#18794E"));
            tvDiasComGastoPessoal.Text = resumo.DiasComGastoPessoal == 0
                ? "NENHUM DIA COM GASTO PESSOAL"
                : $"GASTOS PESSOAIS EM {resumo.DiasComGastoPessoal} {Pluralizar(resumo.DiasComGastoPessoal, "DIA", "DIAS")}";

            tvContasPendentes.Text = resumo.TotalContas > 0
                ? $"QUITADAS: {resumo.ContasQuitadas} DE {resumo.TotalContas} · {percentualContasQuitadas:N0}%"
                : "NENHUMA CONTA PLANEJADA";
            tvDetalhePendencias.Text = $"PARCIAIS: {resumo.ContasParciais} · SEM PAGAMENTO: {resumo.ContasSemPagamento}";
            tvContasVencidas.Text = resumo.ContasVencidas > 0
                ? $"VENCIDAS: {resumo.ContasVencidas} · {FormatarMoeda(resumo.ValorVencido)} EM ATRASO"
                : "NENHUMA CONTA VENCIDA";
            tvContasVencidas.SetTextColor(Color.ParseColor(resumo.ContasVencidas > 0 ? "#C2414F" : "#18794E"));

            tvMaiorCategoria.Text = string.IsNullOrEmpty(resumo.MaiorCategoria)
                ? "MAIOR CATEGORIA\nSEM SAÍDAS NO MÊS"
                : $"MAIOR CATEGORIA\n{resumo.MaiorCategoria} · {FormatarMoeda(resumo.MaiorCategoriaValor)} · {resumo.ParticipacaoMaiorCategoria:N0}%";
            tvConcentracaoCategorias.Text = resumo.QuantidadeTransacoes > 0
                ? $"AS 3 MAIORES CATEGORIAS CONCENTRAM {resumo.ConcentracaoTopTres:N0}% DAS SAÍDAS"
                : "CONCENTRAÇÃO POR CATEGORIA: SEM DADOS";
            tvTicketMedio.Text = $"TICKET MÉDIO\n{FormatarMoeda(resumo.TicketMedio)} · {resumo.QuantidadeTransacoes} {Pluralizar(resumo.QuantidadeTransacoes, "LANÇAMENTO", "LANÇAMENTOS")}";
            tvMaiorLancamento.Text = resumo.QuantidadeTransacoes > 0
                ? $"MAIOR LANÇAMENTO\n{Truncar(resumo.MaiorLancamentoDescricao, 34)} · {FormatarMoeda(resumo.MaiorLancamento)}"
                : "MAIOR LANÇAMENTO\nSEM SAÍDAS NO MÊS";
            tvDiasComSaidas.Text = resumo.DiasComSaidas == 0
                ? "NENHUM DIA COM SAÍDAS"
                : $"HOUVE SAÍDA EM {resumo.DiasComSaidas} {Pluralizar(resumo.DiasComSaidas, "DIA", "DIAS")} DO MÊS";

            progDespesa.Progress = Percentual(resumo.TotalQuitadoCompetencia, resumo.TotalPlanejado);
            progQuitacao.Progress = Percentual(resumo.ContasQuitadas, resumo.TotalContas);
            progDespesa.ContentDescription = resumo.TotalPlanejado > 0m
                ? $"Valor quitado da competência: {percentualQuitado:N0} por cento"
                : "Nenhum compromisso planejado";
            progQuitacao.ContentDescription = resumo.TotalContas > 0
                ? $"Contas totalmente quitadas: {percentualContasQuitadas:N0} por cento"
                : "Nenhuma conta planejada";
            AtualizarSituacao(resumo);
        }

        private void AtualizarSituacao(BalancoMensalResumo resumo)
        {
            string texto;
            string cor;

            if (resumo.TotalReceita <= 0m)
            {
                texto = "SEM RECEITA CADASTRADA PARA ESTA COMPETÊNCIA";
                cor = "#667785";
            }
            else if (resumo.SaldoPlanejado < 0m)
            {
                texto = $"COMPROMISSOS SUPERAM A RECEITA EM {FormatarMoeda(Math.Abs(resumo.SaldoPlanejado))}";
                cor = "#C2414F";
            }
            else if (resumo.ContasVencidas > 0)
            {
                texto = $"{resumo.ContasVencidas} {Pluralizar(resumo.ContasVencidas, "CONTA VENCIDA", "CONTAS VENCIDAS")} · {FormatarMoeda(resumo.ValorVencido)} EM ATRASO";
                cor = "#C2414F";
            }
            else if (resumo.TotalEmAberto > 0m)
            {
                texto = $"{FormatarMoeda(resumo.TotalEmAberto)} AINDA PRECISAM SER QUITADOS";
                cor = "#815300";
            }
            else if (resumo.TotalPlanejado <= 0m)
            {
                texto = "NENHUM COMPROMISSO PLANEJADO PARA O MÊS";
                cor = "#667785";
            }
            else
            {
                texto = "TODOS OS COMPROMISSOS DA COMPETÊNCIA FORAM QUITADOS";
                cor = "#18794E";
            }

            tvResumo.Text = texto;
            tvResumo.SetTextColor(Color.ParseColor(cor));
            tvSaldo.SetTextColor(Color.ParseColor(resumo.SaldoPlanejado < 0m ? "#C2414F" : "#17212B"));
        }

        private void AtualizarCategorias(IReadOnlyList<CategoriaBalanco> categorias)
        {
            layoutCategorias.RemoveAllViews();

            if (categorias.Count == 0)
            {
                var vazio = new TextView(this)
                {
                    Text = "Nenhuma despesa planejada nesta competência.",
                    TextSize = 14f
                };
                vazio.SetTextColor(Color.ParseColor("#667785"));
                layoutCategorias.AddView(vazio);
                return;
            }

            var exibidas = categorias.Take(5).ToList();
            if (categorias.Count > 5)
            {
                exibidas.Add(new CategoriaBalanco
                {
                    Nome = "OUTRAS",
                    Planejado = categorias.Skip(5).Sum(item => item.Planejado),
                    Quitado = categorias.Skip(5).Sum(item => item.Quitado)
                });
            }

            foreach (var categoria in exibidas)
            {
                var percentual = PercentualDecimal(categoria.Quitado, categoria.Planejado);
                var titulo = new TextView(this)
                {
                    Text = categoria.Nome,
                    TextSize = 14f
                };
                titulo.SetTypeface(null, TypefaceStyle.Bold);
                titulo.SetTextColor(Color.ParseColor("#17212B"));

                var detalhe = new TextView(this)
                {
                    Text = $"{FormatarMoeda(categoria.Quitado)} de {FormatarMoeda(categoria.Planejado)} quitados · {percentual:N0}%",
                    TextSize = 12f
                };
                detalhe.SetTextColor(Color.ParseColor("#667785"));

                var progresso = new ProgressBar(this, null, Android.Resource.Attribute.ProgressBarStyleHorizontal)
                {
                    Max = 100,
                    Progress = Percentual(categoria.Quitado, categoria.Planejado),
                    ProgressTintList = ColorStateList.ValueOf(Color.ParseColor("#18794E")),
                    ProgressBackgroundTintList = ColorStateList.ValueOf(Color.ParseColor("#ECF2F6")),
                    ContentDescription = $"{categoria.Nome}: {percentual:N0} por cento quitado"
                };
                var parametros = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(8))
                {
                    TopMargin = Dp(6),
                    BottomMargin = Dp(16)
                };

                layoutCategorias.AddView(titulo);
                layoutCategorias.AddView(detalhe);
                layoutCategorias.AddView(progresso, parametros);
            }
        }

        private static bool CategoriaEh(string? categoria, params string[] categorias)
        {
            var normalizada = BalancoMensalCalculator.NormalizarCategoria(categoria);
            return categorias.Any(c => string.Equals(normalizada, c, StringComparison.OrdinalIgnoreCase));
        }

        private static int Percentual(decimal realizado, decimal planejado)
        {
            return planejado > 0m
                ? Math.Clamp(
                    (int)Math.Round(realizado / planejado * 100m, MidpointRounding.AwayFromZero),
                    0,
                    100)
                : realizado > 0m ? 100 : 0;
        }

        private static decimal PercentualDecimal(decimal realizado, decimal planejado)
        {
            return planejado > 0m ? realizado / planejado * 100m : 0m;
        }

        private static string FormatarMoeda(decimal valor)
        {
            return valor.ToString("C2", CulturaPtBr);
        }

        private static string FormatarProjecaoPessoal(BalancoMensalResumo resumo)
        {
            return resumo.Periodo switch
            {
                PeriodoBalanco.Atual when resumo.ProjecaoPessoal.HasValue =>
                    $"PROJEÇÃO PESSOAL ATÉ O FIM DO MÊS: {FormatarMoeda(resumo.ProjecaoPessoal.Value)}",
                PeriodoBalanco.Passado =>
                    $"PESSOAL PAGO NO MÊS: {FormatarMoeda(resumo.PessoalPagoNoMes)}",
                _ => "PROJEÇÃO PESSOAL DISPONÍVEL APÓS O INÍCIO DO MÊS"
            };
        }

        private static string FormatarRitmoPessoal(BalancoMensalResumo resumo)
        {
            if (resumo.PessoalPlanejado <= 0m)
            {
                return resumo.PessoalPagoNoMes > 0m
                    ? "HÁ GASTOS PESSOAIS, MAS NÃO HÁ ORÇAMENTO PESSOAL PLANEJADO"
                    : "NENHUM ORÇAMENTO PESSOAL PLANEJADO";
            }

            if (resumo.Periodo != PeriodoBalanco.Atual || !resumo.ProjecaoPessoal.HasValue)
            {
                return $"ORÇAMENTO PESSOAL: {FormatarMoeda(resumo.PessoalPlanejado)}";
            }

            var diferenca = resumo.ProjecaoPessoal.Value - resumo.PessoalPlanejado;
            if (Math.Abs(diferenca) < 0.01m)
            {
                return "RITMO ALINHADO AO ORÇAMENTO PESSOAL";
            }

            return diferenca > 0m
                ? $"RITMO ACELERADO: PROJEÇÃO {FormatarMoeda(diferenca)} ACIMA DO ORÇAMENTO"
                : $"RITMO CONTROLADO: PROJEÇÃO {FormatarMoeda(Math.Abs(diferenca))} ABAIXO DO ORÇAMENTO";
        }

        private static string Pluralizar(int quantidade, string singular, string plural)
        {
            return quantidade == 1 ? singular : plural;
        }

        private static string Truncar(string valor, int tamanhoMaximo)
        {
            return valor.Length <= tamanhoMaximo
                ? valor
                : string.Concat(valor.AsSpan(0, tamanhoMaximo - 1), "…");
        }

        private int Dp(int valor)
        {
            return (int)Math.Round(valor * Resources.DisplayMetrics!.Density);
        }

        public static void ConfigurarPieChartDespesas(Context context, PieChart chart, List<Despesa> despesas, int mes, int ano)
        {
            var despesasPorCategoria = despesas
                .Where(d => d.Vencimento.Month == mes && d.Vencimento.Year == ano)
                .GroupBy(d => BalancoMensalCalculator.NormalizarCategoria(d.Categoria))
                .Select(g => new { Categoria = g.Key, Total = g.Sum(x => x.Valor) })
                .OrderByDescending(item => item.Total)
                .ToList();

            if (despesasPorCategoria.Count == 0)
            {
                chart.Clear();
                chart.SetNoDataText("Nenhuma despesa planejada neste mês.");
                chart.ContentDescription = "Nenhuma despesa planejada neste mês.";
                chart.Invalidate();
                return;
            }

            var totalDespesaMes = despesasPorCategoria.Sum(d => d.Total);
            if (totalDespesaMes <= 0m)
            {
                chart.Clear();
                chart.SetNoDataText("Nenhuma despesa planejada neste mês.");
                chart.ContentDescription = "Nenhuma despesa planejada neste mês.";
                chart.Invalidate();
                return;
            }

            var maiores = despesasPorCategoria
                .Where(d => d.Total / totalDespesaMes >= 0.05m)
                .ToList();

            var menores = despesasPorCategoria
                .Where(d => d.Total / totalDespesaMes < 0.05m)
                .ToList();

            var entries = new List<PieEntry>();
            foreach (var item in maiores)
                entries.Add(new PieEntry((float)item.Total, item.Categoria));

            if (menores.Count > 0)
                entries.Add(new PieEntry((float)menores.Sum(x => x.Total), "OUTRAS"));

            var dataSet = new PieDataSet(entries, "Despesas por Categoria");
            dataSet.SetColors(new int[]
            {
                Color.ParseColor("#B42335"),
                Color.ParseColor("#1D5F8A"),
                Color.ParseColor("#18794E"),
                Color.ParseColor("#8A4B08"),
                Color.ParseColor("#6B3FA0"),
                Color.ParseColor("#126A67"),
                Color.ParseColor("#A3255F"),
                Color.ParseColor("#3F51B5"),
                Color.ParseColor("#596B10"),
                Color.ParseColor("#A13C12")
            });

            dataSet.ValueTextSize = 12f;
            dataSet.ValueTextColor = Color.White;

            var data = new PieData(dataSet);
            data.SetValueFormatter(new PercentFormatter(chart));
            chart.Data = data;
            var resumoCategorias = maiores
                .Select(item => $"{item.Categoria}, {PercentualDecimal(item.Total, totalDespesaMes):N0} por cento")
                .ToList();
            if (menores.Count > 0)
            {
                resumoCategorias.Add($"Outras, {PercentualDecimal(menores.Sum(item => item.Total), totalDespesaMes):N0} por cento");
            }

            chart.ContentDescription = $"Distribuição de {FormatarMoeda(totalDespesaMes)}. {string.Join("; ", resumoCategorias)}.";
            chart.Description.Enabled = false;
            chart.SetUsePercentValues(true);
            chart.SetDrawEntryLabels(false);
            chart.Legend.Enabled = true;
            chart.Legend.WordWrapEnabled = true;
            chart.Legend.TextSize = 11f;

            chart.AnimateY(1400, Easing.EaseInOutQuad);
            chart.Invalidate();

            chart.SetOnChartValueSelectedListener(new PieChartListener(context, despesas, mes, ano));
        }

        public static void ConfigurarBarChartSemanalPorTransacoes(
            BarChart chart,
            List<Transacao> transacoes,
            int mes,
            int ano,
            int[] idsDespesasPessoais)
        {
            var idsPermitidos = idsDespesasPessoais.ToHashSet();
            var transacoesFiltradas = transacoes
                .Where(t => t.Data.Month == mes && t.Data.Year == ano &&
                            idsPermitidos.Contains(t.DespesaId) &&
                            t.Despesa != null && CategoriaEh(t.Despesa.Categoria, "PESSOAL"))
                .ToList();

            if (transacoesFiltradas.Count == 0)
            {
                chart.Clear();
                chart.SetNoDataText("Nenhum gasto pessoal pago neste mês.");
                chart.ContentDescription = "Nenhum gasto pessoal pago neste mês.";
                chart.Invalidate();
                return;
            }

            int GetWeekOfMonth(DateTime date)
            {
                var firstDay = new DateTime(date.Year, date.Month, 1);
                int firstWeekOffset = (int)firstDay.DayOfWeek;
                return ((date.Day + firstWeekOffset - 1) / 7) + 1;
            }

            var transacoesPorSemana = transacoesFiltradas
                .GroupBy(t => GetWeekOfMonth(t.Data))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Valor));

            int semanasNoMes = ((DateTime.DaysInMonth(ano, mes) + (int)new DateTime(ano, mes, 1).DayOfWeek - 1) / 7) + 1;

            var barEntries = new List<BarEntry>();
            for (int semana = 1; semana <= semanasNoMes; semana++)
            {
                float total = transacoesPorSemana.TryGetValue(semana, out var valorSemana)
                    ? (float)valorSemana
                    : 0f;
                barEntries.Add(new BarEntry(semana - 1, total)); // <- X começa em 0
            }

            // Dataset
            var barDataSet = new BarDataSet(barEntries, "TRANSAÇÕES DE GASTOS PESSOAIS");
            barDataSet.SetColors(Color.ParseColor("#2196F3"));
            barDataSet.ValueTextSize = 12f;

            var barData = new BarData(barDataSet);
            barData.BarWidth = 0.6f;
            chart.Data = barData;

            // Configurar XAxis
            var xAxis = chart.XAxis;
            xAxis.Position = XAxis.XAxisPosition.Bottom;
            xAxis.Granularity = 1f;
            xAxis.SetDrawGridLines(false);
            xAxis.ValueFormatter = new IndexAxisValueFormatter(
                Enumerable.Range(1, semanasNoMes).Select(s => $"S{s}").ToArray()
            );

            chart.Description.Enabled = false;
            var resumoSemanas = Enumerable.Range(1, semanasNoMes)
                .Select(semana =>
                    $"semana {semana}, {FormatarMoeda(transacoesPorSemana.TryGetValue(semana, out var totalSemana) ? totalSemana : 0m)}");
            chart.ContentDescription = $"Gastos pessoais pagos em {transacoesFiltradas.Count} lançamentos. {string.Join("; ", resumoSemanas)}.";
            chart.SetFitBars(true);
            chart.AxisLeft.AxisMinimum = 0f;
            chart.AxisRight.Enabled = false;

            chart.AnimateY(1200, Easing.EaseInOutQuad);
            chart.Invalidate();

            chart.SetOnChartValueSelectedListener(new BarChartListener(
                chart.Context!,
                mes,
                ano,
                idsDespesasPessoais));

        }
    }

    public class PieChartListener : Java.Lang.Object, IOnChartValueSelectedListenerSupport
    {
        private readonly Context _context;
        private readonly List<Despesa> _despesas;
        private readonly int _mes;
        private readonly int _ano;

        public PieChartListener(Context context, List<Despesa> despesas, int mes, int ano)
        {
            _context = context;
            _despesas = despesas;
            _mes = mes;
            _ano = ano;
        }

        public void OnNothingSelected() { }

        public void OnValueSelected(Entry e, Highlight h)
        {
            if (e is PieEntry pieEntry)
            {
                string categoria = pieEntry.Label;
                var intent = new Intent(_context, typeof(TransacaoListActivity));
                intent.PutExtra("Categoria", categoria);
                intent.PutExtra("Mes", _mes);
                intent.PutExtra("Ano", _ano);

                if (string.Equals(categoria, "OUTRAS", StringComparison.OrdinalIgnoreCase))
                {
                    intent.PutExtra("Categorias", ObterCategoriasOutras());
                }

                _context.StartActivity(intent);
            }
        }

        private string[] ObterCategoriasOutras()
        {
            var despesasPorCategoria = _despesas
                .Where(d => d.Vencimento.Month == _mes && d.Vencimento.Year == _ano)
                .GroupBy(d => BalancoMensalCalculator.NormalizarCategoria(d.Categoria))
                .Select(g => new { Categoria = g.Key, Total = g.Sum(x => x.Valor) })
                .ToList();

            var totalDespesaMes = despesasPorCategoria.Sum(d => d.Total);

            if (totalDespesaMes == 0)
            {
                return Array.Empty<string>();
            }

            return despesasPorCategoria
                .Where(d => d.Total / totalDespesaMes < 0.05m)
                .Select(d => d.Categoria)
                .ToArray();
        }
    }

    public class BarChartListener : Java.Lang.Object, IOnChartValueSelectedListenerSupport
    {
        private readonly Context _context;
        private readonly int _mes;
        private readonly int _ano;
        private readonly int[] _idsDespesasPessoais;

        public BarChartListener(Context context, int mes, int ano, int[] idsDespesasPessoais)
        {
            _context = context;
            _mes = mes;
            _ano = ano;
            _idsDespesasPessoais = idsDespesasPessoais;
        }

        public void OnNothingSelected() { }

        public void OnValueSelected(Entry e, Highlight h)
        {
            if (e is BarEntry barEntry)
            {
                int semana = (int)barEntry.GetX() + 1; // X começa em 0, então +1

                var intent = new Intent(_context, typeof(TransacaoListActivity));
                intent.PutExtra("Mes", _mes);
                intent.PutExtra("Ano", _ano);
                intent.PutExtra("Semana", semana);
                intent.PutExtra("DespesaIds", _idsDespesasPessoais);
                _context.StartActivity(intent);
            }
        }
    }

    public class SwipeGestureListener : GestureDetector.SimpleOnGestureListener
    {
        private readonly Func<Task> OnSwipeRight;
        private readonly Func<Task> OnSwipeLeft;

        private const int SWIPE_THRESHOLD = 100;
        private const int SWIPE_VELOCITY_THRESHOLD = 100;

        public SwipeGestureListener(Func<Task> onSwipeRight, Func<Task> onSwipeLeft)
        {
            OnSwipeRight = onSwipeRight;
            OnSwipeLeft = onSwipeLeft;
        }

        public override bool OnFling(MotionEvent? e1, MotionEvent? e2, float velocityX, float velocityY)
        {
            if (e1 == null || e2 == null)
            {
                return false;
            }

            float diffX = e2.GetX() - e1.GetX();
            float diffY = e2.GetY() - e1.GetY();

            if (Math.Abs(diffX) > Math.Abs(diffY))
            {
                if (Math.Abs(diffX) > SWIPE_THRESHOLD && Math.Abs(velocityX) > SWIPE_VELOCITY_THRESHOLD)
                {
                    if (diffX > 0)
                        OnSwipeRight?.Invoke();
                    else
                        OnSwipeLeft?.Invoke();

                    return true;
                }
            }
            return false;
        }
    }
}

