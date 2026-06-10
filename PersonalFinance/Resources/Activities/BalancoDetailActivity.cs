using Android.Content;
using Android.Graphics;
using Android.Views;
using MikePhil.Charting.Animation;
using MikePhil.Charting.Charts;
using MikePhil.Charting.Components;
using MikePhil.Charting.Data;
using MikePhil.Charting.Formatter;
using MikePhil.Charting.Highlight;
using MikePhil.Charting.Listener;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Balanço Mensal")]
    public class BalancoDetailActivity : Activity
    {
        private TextView tvTotalReceita, tvTotalDespesa, tvSaldo, tvTotalQuitado, tvFaltaQuitar, tvResumo;
        private TextView tvGastosPessoais, tvGastosDomesticos, tvCombustivel, tvSaldoPlanejado, tvSaldoRealizado;
        private TextView tvDesvioOrcamento, tvComprometimentoRenda, tvContasPendentes, tvMediaDiaria, tvLimiteDiario, tvMaiorCategoria, tvMesAtual;
        private ProgressBar progDespesa, progGastosDomesticos, progGastosPessoais, progCombustivel;
        private PieChart pieChartDespesas;
        private BarChart barChartSemanal;

        private DatabaseService _db;
        private List<Despesa> _despesas;

        private GestureDetector _gestureDetector;
        private LinearLayout _rootLayout; // Layout raiz para capturar o toque

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_balanco);

            // Inicializar TextViews
            tvMesAtual = FindViewById<TextView>(Resource.Id.tvMesAtual);
            tvTotalReceita = FindViewById<TextView>(Resource.Id.tvTotalReceita);
            tvTotalDespesa = FindViewById<TextView>(Resource.Id.tvTotalDespesa);
            tvTotalQuitado = FindViewById<TextView>(Resource.Id.tvTotalQuitado);
            tvGastosPessoais = FindViewById<TextView>(Resource.Id.tvGastosPessoais);
            tvGastosDomesticos = FindViewById<TextView>(Resource.Id.tvGastosDomesticos);
            tvCombustivel = FindViewById<TextView>(Resource.Id.tvCombustivel);
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
            //tvMaiorCategoria = FindViewById<TextView>(Resource.Id.tvMaiorCategoria);

            // ProgressBars
            progDespesa = FindViewById<ProgressBar>(Resource.Id.progDespesa);
            progGastosPessoais = FindViewById<ProgressBar>(Resource.Id.progGastosPessoais);
            progGastosDomesticos = FindViewById<ProgressBar>(Resource.Id.progGastosDomesticos);
            progCombustivel = FindViewById<ProgressBar>(Resource.Id.progCombustivel);

            // Gráficos
            pieChartDespesas = FindViewById<PieChart>(Resource.Id.pieChartDespesas);
            barChartSemanal = FindViewById<BarChart>(Resource.Id.barChartSemanal);

            _db = new DatabaseService();

            var mesRef = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var dtInicio = new DateTime(mesRef.Year, mesRef.Month, 1);
            var dtFinal = new DateTime(mesRef.Year, mesRef.Month, DateTime.DaysInMonth(mesRef.Year, mesRef.Month));

            // Gesture detector
            _gestureDetector = new GestureDetector(this, new SwipeGestureListener(
                onSwipeRight: async () =>
                {
                    mesRef = mesRef.AddMonths(-1);
                    dtInicio = new DateTime(mesRef.Year, mesRef.Month, 1);
                    dtFinal = new DateTime(mesRef.Year, mesRef.Month, DateTime.DaysInMonth(mesRef.Year, mesRef.Month));
                    await CarregarDadosAsync(dtInicio, dtFinal);
                },
                onSwipeLeft: async () =>
                {
                    mesRef = mesRef.AddMonths(1);
                    dtInicio = new DateTime(mesRef.Year, mesRef.Month, 1);
                    dtFinal = new DateTime(mesRef.Year, mesRef.Month, DateTime.DaysInMonth(mesRef.Year, mesRef.Month));
                    await CarregarDadosAsync(dtInicio, dtFinal);
                }
            ));

            // Layout raiz para capturar toque
            _rootLayout = FindViewById<LinearLayout>(Resource.Id.rootLayout);
            _rootLayout.Touch += (s, e) =>
            {
                _gestureDetector.OnTouchEvent(e.Event);
            };


            await CarregarDadosAsync(dtInicio, dtFinal);
        }

        public override bool OnTouchEvent(MotionEvent e)
        {
            _gestureDetector?.OnTouchEvent(e);
            return base.OnTouchEvent(e);
        }


        private async Task CarregarDadosAsync(DateTime dtInicio, DateTime dtFinal)
        {
            var despesas = await _db.ListaDespesasAsync(dtInicio, dtFinal) ?? new List<Despesa>();
            var receitas = await _db.ListaReceitasAsync(dtInicio, dtFinal) ?? new List<Receita>();
            var transacoes = await _db.ListaTransacoesAsync(dtInicio, dtFinal) ?? new List<Transacao>();

            _despesas = despesas;

            //var hoje = DateTime.Today;
            var mes = dtInicio.Month;
            var ano = dtInicio.Year;

            tvMesAtual.Text = dtInicio.ToString("MMMM 'de' yyyy").ToUpper();

            //---------------------------------------------------------
            // Atualizar indicadores
            AtualizarIndicadores(receitas, despesas, transacoes, mes, ano);

            //---------------------------------------------------------
            // Gráfico de despesas por categoria
            ConfigurarPieChartDespesas(this, pieChartDespesas, _despesas, mes, ano);

            //---------------------------------------------------------
            // Gráfico semanal por transações
            ConfigurarBarChartSemanalPorTransacoes(barChartSemanal, transacoes, mes, ano);
        }

        private void AtualizarIndicadores(List<Receita> receitas, List<Despesa> despesas, List<Transacao> transacoes, int mes, int ano)
        {
            var despesasMes = despesas
                .Where(d => d.Vencimento.Month == mes && d.Vencimento.Year == ano)
                .ToList();

            var transacoesMes = transacoes
                .Where(t => t.Data.Month == mes && t.Data.Year == ano)
                .ToList();

            var totalReceita = receitas
                .Where(r => r.MesReferencia.Month == mes && r.MesReferencia.Year == ano)
                .Sum(r => r?.Valor ?? 0);

            var totalDespesa = despesasMes.Sum(d => d.Valor);
            var totalQuitado = transacoesMes.Sum(t => t?.Valor ?? 0);
            var faltaQuitar = Math.Max(0m, totalDespesa - totalQuitado);
            var desvioOrcamento = totalQuitado - totalDespesa;
            var saldoPlanejado = totalReceita - totalDespesa;
            var saldoRealizado = totalReceita - totalQuitado;
            var saldoAposCompromissos = totalReceita - Math.Max(totalDespesa, totalQuitado);
            var gastoPessoalDiarioPlan = Planejado("PESSOAL") / DateTime.DaysInMonth(ano, mes);

            decimal Planejado(params string[] categorias) => despesasMes.Where(d => CategoriaEh(d.Categoria, categorias)).Sum(d => d.Valor);
            decimal Realizado(params string[] categorias) => transacoesMes.Where(t => t.Despesa != null && CategoriaEh(t.Despesa.Categoria, categorias)).Sum(t => t.Valor);

            var gastoPessoalPlanejado = Planejado("PESSOAL");
            var gastoPessoal = Realizado("PESSOAL");
            var gastoDomesticoPlanejado = Planejado("CASA", "GASTOS DOMESTICOS");
            var gastoDomestico = Realizado("CASA", "GASTOS DOMESTICOS");
            var combustivelPlanejado = Planejado("COMBUSTIVEL");
            var combustivelRealizado = Realizado("COMBUSTIVEL");

            var pagosPorDespesa = transacoesMes
                .GroupBy(t => t.DespesaId)
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Valor));

            var contasPendentes = despesasMes.Count(d =>
                !pagosPorDespesa.TryGetValue(d.Id, out decimal pago) || pago < d.Valor);

            var categoriasRealizadas = transacoesMes
                .Where(t => t.Despesa != null)
                .GroupBy(t => NormalizarCategoria(t.Despesa.Categoria))
                .Select(g => new { Categoria = g.Key, Total = g.Sum(t => t.Valor) })
                .OrderByDescending(x => x.Total)
                .FirstOrDefault();

            var hoje = DateTime.Today;
            var inicioMesAtual = new DateTime(hoje.Year, hoje.Month, 1);
            var inicioMesIndicador = new DateTime(ano, mes, 1);
            var diasNoMes = DateTime.DaysInMonth(ano, mes);
            var diasDecorridos = inicioMesIndicador < inicioMesAtual
                ? diasNoMes
                : inicioMesIndicador == inicioMesAtual ? hoje.Day : 0;
            var diasRestantes = inicioMesIndicador < inicioMesAtual
                ? 0
                : inicioMesIndicador == inicioMesAtual ? diasNoMes - hoje.Day + 1 : diasNoMes;
            var mediaDiaria = diasDecorridos > 0 ? totalQuitado / diasDecorridos : 0;
            var limiteDiario = diasRestantes > 0 ? Math.Max(0m, saldoAposCompromissos) / diasRestantes : 0;
            var comprometimento = totalReceita > 0 ? totalDespesa / totalReceita * 100 : 0;

            tvSaldo.Text = $"SALDO APOS COMPROMISSOS: R$ {saldoAposCompromissos:N2}";
            tvTotalDespesa.Text = FormatarAcompanhamento("TOTAL DAS DESPESAS", totalDespesa, totalQuitado);
            tvGastosPessoais.Text = FormatarAcompanhamento("PESSOAL", gastoPessoalPlanejado, gastoPessoal);
            tvGastosDomesticos.Text = FormatarAcompanhamento("CASA / DOMESTICOS", gastoDomesticoPlanejado, gastoDomestico);
            tvCombustivel.Text = FormatarAcompanhamento("COMBUSTIVEL", combustivelPlanejado, combustivelRealizado);

            tvTotalReceita.Text = $"RECEITA DO MES: R$ {totalReceita:N2}";
            tvTotalQuitado.Text = $"TOTAL REALIZADO: R$ {totalQuitado:N2}";
            tvFaltaQuitar.Text = $"PREVISTO EM ABERTO: R$ {faltaQuitar:N2}";
            tvSaldoPlanejado.Text = $"SALDO PLANEJADO: R$ {saldoPlanejado:N2}";
            tvSaldoRealizado.Text = $"SALDO REALIZADO: R$ {saldoRealizado:N2}";
            tvDesvioOrcamento.Text = desvioOrcamento > 0 ? $"ACIMA DO PLANEJADO: R$ {desvioOrcamento:N2}" : $"ABAIXO DO PLANEJADO: R$ {Math.Abs(desvioOrcamento):N2}";
            tvComprometimentoRenda.Text = $"COMPROMETIMENTO DA RENDA: {comprometimento:N1}%";
            tvContasPendentes.Text = $"CONTAS PENDENTES: {contasPendentes} DE {despesasMes.Count}";
            tvMediaDiaria.Text = $"MEDIA DIARIA REALIZADA: R$ {mediaDiaria:N2}";
            tvLimiteDiario.Text = diasRestantes > 0 ? $"DISPONIVEL POR DIA ({diasRestantes} DIAS): R$ {gastoPessoalDiarioPlan:N2}" : "DISPONIVEL POR DIA: MES ENCERRADO";
            tvMaiorCategoria.Text = categoriasRealizadas == null ? "MAIOR CONSUMO: SEM TRANSACOES" : $"MAIOR CONSUMO: {categoriasRealizadas.Categoria} - R$ {categoriasRealizadas.Total:N2}";

            progDespesa.Progress = Percentual(totalQuitado, totalDespesa);
            progGastosPessoais.Progress = Percentual(gastoPessoal, gastoPessoalPlanejado);
            progGastosDomesticos.Progress = Percentual(gastoDomestico, gastoDomesticoPlanejado);
            progCombustivel.Progress = Percentual(combustivelRealizado, combustivelPlanejado);

            if (totalReceita == 0)
            {
                tvResumo.Text = "SEM RECEITA REGISTRADA!";
                tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#667785")); // cinza
            }
            else if (saldoAposCompromissos < 0)
            {
                tvResumo.Text = $"ORCAMENTO NO VERMELHO EM R$ {Math.Abs(saldoAposCompromissos):N2}";
                tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#C2414F"));
            }
            else if (desvioOrcamento > 0)
            {
                tvResumo.Text = $"GASTOS R$ {desvioOrcamento:N2} ACIMA DO PLANEJADO";
                tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#B7791F"));
            }
            else if (contasPendentes > 0)
            {
                tvResumo.Text = $"{contasPendentes} CONTAS AINDA AGUARDAM PAGAMENTO";
                tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#B7791F"));
            }
            else
            {
                tvResumo.Text = "TODAS AS DESPESAS PLANEJADAS FORAM ATENDIDAS";
                tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#18794E"));
            }
        }

        private static bool CategoriaEh(string? categoria, params string[] categorias)
        {
            var normalizada = NormalizarCategoria(categoria);
            return categorias.Any(c => string.Equals(normalizada, c, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizarCategoria(string? categoria)
        {
            return string.IsNullOrWhiteSpace(categoria) ? "OUTROS" : categoria.Trim().ToUpperInvariant();
        }

        private static int Percentual(decimal realizado, decimal planejado)
        {
            return planejado > 0
                ? Math.Clamp((int)(realizado / planejado * 100), 0, 100)
                : realizado > 0 ? 100 : 0;
        }

        private static string FormatarAcompanhamento(string titulo, decimal planejado, decimal realizado)
        {
            if (planejado <= 0)
            {
                return realizado > 0
                    ? $"{titulo}: R$ {realizado:N2} | SEM VALOR PLANEJADO"
                    : $"{titulo}: SEM VALOR PLANEJADO";
            }

            var diferenca = planejado - realizado;
            var percentual = realizado / planejado * 100;
            var complemento = diferenca >= 0
                ? $"restam R$ {diferenca:N2}"
                : $"excedeu R$ {Math.Abs(diferenca):N2}";

            return $"{titulo}: {percentual:N0}% | R$ {realizado:N2} DE R$ {planejado:N2} | {complemento}";
        }

        public void ConfigurarPieChartDespesas(Context context, PieChart chart, List<Despesa> despesas, int mes, int ano)
        {
            var despesasPorCategoria = despesas
                .Where(d => d.Vencimento.Month == mes && d.Vencimento.Year == ano)
                .GroupBy(d => d.Categoria ?? "Outros")
                .Select(g => new { Categoria = g.Key, Total = g.Sum(x => x.Valor) })
                .ToList();

            if (!despesasPorCategoria.Any()) { chart.Clear(); return; }

            var totalDespesaMes = despesasPorCategoria.Sum(d => d.Total);
            if (totalDespesaMes <= 0) { chart.Clear(); return; }

            var maiores = despesasPorCategoria
                .Where(d => d.Total / totalDespesaMes >= 0.05m)
                .ToList();

            var menores = despesasPorCategoria
                .Where(d => d.Total / totalDespesaMes < 0.05m)
                .ToList();

            var entries = new List<PieEntry>();
            foreach (var item in maiores)
                entries.Add(new PieEntry((float)item.Total, item.Categoria));

            if (menores.Any())
                entries.Add(new PieEntry((float)menores.Sum(x => x.Total), "OUTRAS"));

            var dataSet = new PieDataSet(entries, "Despesas por Categoria");
            dataSet.SetColors(new int[]
            {
                Color.ParseColor("#F44336"),
                Color.ParseColor("#2196F3"),
                Color.ParseColor("#4CAF50"),
                Color.ParseColor("#FF9800"),
                Color.ParseColor("#9C27B0"),
                Color.ParseColor("#009688"),
                Color.ParseColor("#E91E63"),
                Color.ParseColor("#3F51B5"),
                Color.ParseColor("#CDDC39"),
                Color.ParseColor("#FF5722")
            });

            dataSet.ValueTextSize = 12f;
            dataSet.ValueTextColor = Color.Black;

            var data = new PieData(dataSet);
            chart.Data = data;
            chart.Description.Enabled = false;
            chart.SetUsePercentValues(true);
            chart.SetDrawEntryLabels(true);
            chart.Legend.Enabled = false;

            chart.AnimateY(1400, Easing.EaseInOutQuad);
            chart.Invalidate();

            chart.SetOnChartValueSelectedListener(new PieChartListener(context, despesas, mes, ano));
        }

        public void ConfigurarBarChartSemanalPorTransacoes(BarChart chart, List<Transacao> transacoes, int mes, int ano)
        {
            var transacoesFiltradas = transacoes
                .Where(t => t.Data.Month == mes && t.Data.Year == ano &&
                            t.Despesa != null && CategoriaEh(t.Despesa.Categoria, "PESSOAL"))
                .ToList();

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
                float total = transacoesPorSemana.ContainsKey(semana) ? (float)transacoesPorSemana[semana] : 0f;
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
                Enumerable.Range(1, semanasNoMes).Select(s => $"Semana {s}").ToArray()
            );

            chart.Description.Enabled = false;
            chart.SetFitBars(true);
            chart.AxisLeft.AxisMinimum = 0f;
            chart.AxisRight.Enabled = false;

            chart.AnimateY(1200, Easing.EaseInOutQuad);
            chart.Invalidate();

            chart.SetOnChartValueSelectedListener(new BarChartListener(chart.Context, mes, ano));

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
                .GroupBy(d => d.Categoria ?? "Outros")
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

        public BarChartListener(Context context, int mes, int ano)
        {
            _context = context;
            _mes = mes;
            _ano = ano;
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

        public override bool OnFling(MotionEvent e1, MotionEvent e2, float velocityX, float velocityY)
        {
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

