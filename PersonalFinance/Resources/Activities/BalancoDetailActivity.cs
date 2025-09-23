using Android.Content;
using Android.Graphics;
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
        private TextView tvTotalReceita, tvTotalDespesa, tvSaldo, tvTotalQuitado, tvFaltaQuitar, tvResumo, tvGastosPessoais, tvGastosDomesticos, tvDespesaFixa, tvDespesaFixaQuitada, tvDespFixaAberta;
        private ProgressBar progDespesa, progGastosDomesticos, progGastosPessoais;
        private PieChart pieChartDespesas;
        private BarChart barChartSemanal;

        private DatabaseService _db;
        private List<Despesa> _despesas;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_balanco);

            // Inicializar TextViews
            tvDespesaFixa = FindViewById<TextView>(Resource.Id.tvDespesaFixa);
            tvDespFixaAberta = FindViewById<TextView>(Resource.Id.tvDespFixaAberta);
            tvDespesaFixaQuitada = FindViewById<TextView>(Resource.Id.tvDespesaFixaQuitada);
            tvTotalReceita = FindViewById<TextView>(Resource.Id.tvTotalReceita);
            tvTotalDespesa = FindViewById<TextView>(Resource.Id.tvTotalDespesa);
            tvTotalQuitado = FindViewById<TextView>(Resource.Id.tvTotalQuitado);
            tvGastosPessoais = FindViewById<TextView>(Resource.Id.tvGastosPessoais);
            tvGastosDomesticos = FindViewById<TextView>(Resource.Id.tvGastosDomesticos);
            tvSaldo = FindViewById<TextView>(Resource.Id.tvSaldo);
            tvFaltaQuitar = FindViewById<TextView>(Resource.Id.tvFaltaQuitar);
            tvResumo = FindViewById<TextView>(Resource.Id.tvResumo);

            // ProgressBars
            progDespesa = FindViewById<ProgressBar>(Resource.Id.progDespesa);
            progGastosPessoais = FindViewById<ProgressBar>(Resource.Id.progGastosPessoais);
            progGastosDomesticos = FindViewById<ProgressBar>(Resource.Id.progGastosDomesticos);

            // Gráficos
            pieChartDespesas = FindViewById<PieChart>(Resource.Id.pieChartDespesas);
            barChartSemanal = FindViewById<BarChart>(Resource.Id.barChartSemanal);

            _db = new DatabaseService();

            await CarregarDadosAsync();
        }

        private async Task CarregarDadosAsync()
        {
            var despesas = await _db.ListaDespesasAsync() ?? new List<Despesa>();
            var receitas = await _db.ListaReceitasAsync() ?? new List<Receita>();
            var transacoes = await _db.ListaTransacoesAsync() ?? new List<Transacao>();

            _despesas = despesas;

            var hoje = DateTime.Today;
            var mes = hoje.Month;
            var ano = hoje.Year;

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
            var totalReceita = receitas
                .Where(r => r.MesReferencia != null && r.MesReferencia.Month == mes && r.MesReferencia.Year == ano)
                .Sum(r => r?.Valor ?? 0);

            var totalDespesa = despesas
                .Where(d => d.Vencimento.Month == mes && d.Vencimento.Year == ano)
                .Sum(d => d.Valor);

            var totalQuitado = transacoes
                .Where(t => t.Data.Month == mes && t.Data.Year == ano)
                .Sum(t => t?.Valor ?? 0);

            var gastoPessoalPlanejado = despesas
                .Where(d => (d.Categoria ?? "").Trim().ToUpper() == "PESSOAL")
                .Sum(d => d.Valor);

            var gastoPessoal = transacoes
                .Where(t => t.Despesa != null && (t.Despesa.Categoria ?? "").Trim().ToUpper() == "PESSOAL")
                .Sum(t => t.Valor);

            var gastoDomesticoPlanejado = despesas
                .Where(d => (d.Categoria ?? "").Trim().ToUpper() == "CASA")
                .Sum(d => d.Valor);

            var gastoDomestico = transacoes
                .Where(t => t.Despesa != null && (t.Despesa.Categoria ?? "").Trim().ToUpper() == "CASA")
                .Sum(t => t.Valor);

            decimal despesaFixaQuitada = transacoes.Where(x => !x.Despesa.Categoria.Equals("PESSOAL")
                                    && !x.Despesa.Categoria.Equals("GASTOS DOMESTICOS")).Sum(x => x?.Valor ?? 0);


            var saldo = totalReceita - totalDespesa;

            // Atualiza textos
            tvTotalReceita.Text = $"RECEITA: R${totalReceita:N2}";
            tvTotalDespesa.Text = $"DESPESAS: R${totalDespesa:N2}";
            tvTotalQuitado.Text = $"DESPESAS QUITADAS: R${totalQuitado:N2}";
            tvFaltaQuitar.Text = $"GASTOS PREVISTOS EM ABERTO: R${(totalDespesa - totalQuitado):N2}";
            tvGastosPessoais.Text = $"GASTOS PESSOAIS: R${(gastoPessoalPlanejado - gastoPessoal):N2} DISPONÍVEIS DE R${gastoPessoalPlanejado:N2}";
            tvGastosDomesticos.Text = $"GASTOS DOMESTICOS: R${(gastoDomesticoPlanejado - gastoDomestico):N2} DISPONÍVEIS DE R${gastoDomesticoPlanejado:N2}";
            tvSaldo.Text = $"POUPADO: R$ {saldo:N2}";

            //novos indicadores
            var despesaFixa = totalDespesa - gastoPessoalPlanejado;
            tvDespesaFixa.Text = $"DESPESA FIXA: {despesaFixa:N2}";
            tvDespesaFixaQuitada.Text = $"DESPESA FIXA QUITADA: {despesaFixaQuitada:N2}";
            tvDespFixaAberta.Text = $"DESPESA FIXA ABERTA: {(despesaFixa - despesaFixaQuitada):N2}";

            // Atualiza indicadores
            progDespesa.Progress = totalDespesa > 0 ? (int)((totalQuitado / totalDespesa) * 100) : 0;
            progGastosPessoais.Progress = gastoPessoalPlanejado > 0 ? (int)((gastoPessoal / gastoPessoalPlanejado) * 100) : 0;
            progGastosDomesticos.Progress = gastoDomesticoPlanejado > 0 ? (int)((gastoDomestico / gastoDomesticoPlanejado) * 100) : 0;

            int porcPoupada = 0;

            if (totalReceita == 0)
            {
                tvResumo.Text = "SEM RECEITA REGISTRADA!";
                tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#9E9E9E")); // cinza
            }
            else
            {
                porcPoupada = (int)(((totalReceita - totalDespesa) / totalReceita) * 100);

                if (porcPoupada < 0)
                {
                    tvResumo.Text = "SITUAÇÃO CRÍTICA! GASTOS MAIORES QUE RECEITA!";
                    tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#F44336")); // vermelho
                }
                else if (porcPoupada < 20)
                {
                    tvResumo.Text = $"VOCÊ ESTÁ POUPANDO SOMENTE {porcPoupada}%";
                    tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#FF9800")); // laranja
                }
                else
                {
                    tvResumo.Text = $"VOCÊ ESTÁ POUPANDO {porcPoupada}% 👏";
                    tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#4CAF50")); // verde
                }
            }

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
                .Where(t => t.Data.Month == mes && t.Data.Year == ano && t.Despesa.Categoria == "PESSOAL")
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
                _context.StartActivity(intent);
            }
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

}
