using Android.Content;
using MikePhil.Charting.Animation;
using MikePhil.Charting.Charts;
using MikePhil.Charting.Data;
using MikePhil.Charting.Highlight;
using MikePhil.Charting.Listener; // Add this using directive at the top of the file
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

        private DatabaseService _db;
        private List<Despesa> _despesas; // armazenar despesas do mês

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_balanco);

            // TextViews
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

            //GRAFICOS
            pieChartDespesas = FindViewById<PieChart>(Resource.Id.pieChartDespesas);

            _db = new DatabaseService();

            await CarregarDadosAsync();
        }

        private async Task CarregarDadosAsync()
        {
            var despesas = await _db.ListaDespesasAsync() ?? new List<Despesa>();
            var receitas = await _db.ListaReceitasAsync() ?? new List<Receita>();
            var transacoes = await _db.ListaTransacoesAsync() ?? new List<Transacao>();

            _despesas = despesas; // guardar lista para uso no clique

            var hoje = DateTime.Today;
            var mes = hoje.Month;
            var ano = hoje.Year;

            decimal totalReceita = receitas
                .Where(r => r?.MesReferencia != null && r.MesReferencia.Month == mes && r.MesReferencia.Year == ano)
                .Sum(r => r?.Valor ?? 0);

            decimal totalDespesa = despesas
                .Where(d => d?.Vencimento != null && d.Vencimento.Month == mes && d.Vencimento.Year == ano)
                .Sum(d => d?.Valor ?? 0);

            decimal totalQuitado = transacoes
                .Where(r => r?.Data != null && r.Data.Month == mes && r.Data.Year == ano)
                .Sum(r => r?.Valor ?? 0);

            // Gastos pessoais e domésticos
            decimal gastoPlanejado = despesas
                .Where(r => string.Equals(r?.Categoria, "PESSOAL", StringComparison.OrdinalIgnoreCase))
                .Sum(r => r?.Valor ?? 0);

            decimal gastoPessoal = transacoes
                .Where(r => string.Equals(r?.Despesa?.Categoria, "PESSOAL", StringComparison.OrdinalIgnoreCase))
                .Sum(r => r?.Valor ?? 0);

            decimal gastoDomesticoPlanejado = despesas
                .Where(r => string.Equals(r?.Categoria, "GASTOS DOMESTICOS", StringComparison.OrdinalIgnoreCase))
                .Sum(r => r?.Valor ?? 0);

            decimal gastoDomestico = transacoes
                .Where(r => string.Equals(r?.Despesa?.Categoria, "GASTOS DOMESTICOS", StringComparison.OrdinalIgnoreCase))
                .Sum(r => r?.Valor ?? 0);

            decimal despesaFixaQuitada = transacoes.Where(x => !x.Despesa.Categoria.Equals("PESSOAL")
                                                && !x.Despesa.Categoria.Equals("GASTOS DOMESTICOS")).Sum(x => x?.Valor ?? 0);

            var saldo = totalReceita - totalDespesa;

            // Atualiza textos
            tvTotalReceita.Text = $"RECEITA: R${totalReceita:N2}";
            tvTotalDespesa.Text = $"DESPESAS: R${totalDespesa:N2}";
            tvTotalQuitado.Text = $"DESPESAS QUITADAS: R${totalQuitado:N2}";
            tvFaltaQuitar.Text = $"DESPESAS PREVISTAS EM ABERTO: R${(totalDespesa - totalQuitado):N2}";
            tvGastosPessoais.Text = $"GASTOS PESSOAIS: R${(gastoPlanejado - gastoPessoal):N2} DISPONÍVEIS DE R${gastoPlanejado:N2}";
            tvGastosDomesticos.Text = $"GASTOS DOMESTICOS: R${(gastoDomesticoPlanejado - gastoDomestico):N2} DISPONÍVEIS DE R${gastoDomesticoPlanejado:N2}";
            tvSaldo.Text = $"POUPADO: R$ {saldo:N2}";

            //novos indicadores
            var despesaFixa = totalDespesa - gastoPlanejado;
            tvDespesaFixa.Text = $"DESPESA FIXA: {despesaFixa:N2}";
            tvDespesaFixaQuitada.Text = $"DESPESA FIXA QUITADA: {despesaFixaQuitada:N2}";
            tvDespFixaAberta.Text = $"DESPESA FIXA ABERTA: {(despesaFixa - despesaFixaQuitada):N2}";

            // Atualiza indicadores
            progDespesa.Progress = totalDespesa > 0 ? (int)((totalQuitado / totalDespesa) * 100) : 0;
            progGastosPessoais.Progress = gastoPlanejado > 0 ? (int)((gastoPessoal / gastoPlanejado) * 100) : 0;
            progGastosDomesticos.Progress = gastoDomesticoPlanejado > 0 ? (int)((gastoDomestico / gastoDomesticoPlanejado) * 100) : 0;

            // Resumo
            if (saldo > 0)
            {
                tvResumo.Text = "VOCÊ ESTÁ POSITIVO! 👏";
                tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#4CAF50"));
            }
            else if (saldo == 0)
            {
                tvResumo.Text = "VOCÊ ESTÁ ZERADO!";
                tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#FF9800"));
            }
            else
            {
                tvResumo.Text = "VOCÊ ESTÁ NEGATIVO!";
                tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#F44336"));
            }

            //---------------------------------------------------------
            // Gráfico de despesas por categoria com "Outras" e clique

            var despesasPorCategoria = despesas
                .Where(d => d.Vencimento.Month == mes && d.Vencimento.Year == ano)
                .GroupBy(d => d.Categoria ?? "Outros")
                .Select(g => new { Categoria = g.Key, Total = g.Sum(x => x.Valor) })
                .ToList();

            var totalDespesaMes = despesasPorCategoria.Sum(d => d.Total);

            // Separar categorias maiores e menores que 10%
            var maiores = despesasPorCategoria.Where(d => d.Total / totalDespesaMes >= 0.1m).ToList();
            var menores = despesasPorCategoria.Where(d => d.Total / totalDespesaMes < 0.1m).ToList();

            var entries = new List<PieEntry>();

            foreach (var item in maiores)
                entries.Add(new PieEntry((float)item.Total, item.Categoria));

            if (menores.Any())
                entries.Add(new PieEntry((float)menores.Sum(x => x.Total), "OUTRAS"));

            var dataSet = new PieDataSet(entries, "Despesas por Categoria");

            dataSet.SetColors(new int[]
            {
                Android.Graphics.Color.ParseColor("#F44336"),
                Android.Graphics.Color.ParseColor("#2196F3"),
                Android.Graphics.Color.ParseColor("#4CAF50"),
                Android.Graphics.Color.ParseColor("#FF9800"),
                Android.Graphics.Color.ParseColor("#9C27B0"),
                Android.Graphics.Color.ParseColor("#009688"),
                Android.Graphics.Color.ParseColor("#E91E63"),
                Android.Graphics.Color.ParseColor("#3F51B5"),
                Android.Graphics.Color.ParseColor("#CDDC39"),
                Android.Graphics.Color.ParseColor("#FF5722")
            });

            dataSet.ValueTextSize = 12f;
            dataSet.ValueTextColor = Android.Graphics.Color.Black;

            var data = new PieData(dataSet);
            pieChartDespesas.Data = data;

            pieChartDespesas.Description.Enabled = false;
            pieChartDespesas.SetUsePercentValues(true);
            pieChartDespesas.SetDrawEntryLabels(true);
            pieChartDespesas.Legend.Enabled = false;

            pieChartDespesas.AnimateY(1400, Easing.EaseInOutQuad);
            pieChartDespesas.Invalidate();

            // 🔥 Adiciona o listener para clique
            pieChartDespesas.SetOnChartValueSelectedListener(new PieChartListener(this, _despesas, mes, ano));
        }
    }

    // Listener para capturar clique em fatias
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

                // Filtrar despesas
                var filtradas = _despesas
                    .Where(d => d.Vencimento.Month == _mes && d.Vencimento.Year == _ano)
                    .Where(d =>
                        (categoria == "OUTRAS" && (string.IsNullOrEmpty(d.Categoria) || d.Categoria == "Outros")) ||
                        (categoria != "OUTRAS" && string.Equals(d.Categoria, categoria, StringComparison.OrdinalIgnoreCase))
                    )
                    .ToList();

                // Montar lista em string
                string msg = string.Join("\n", filtradas.Select(d => $"{d.Descricao}: R${d.Valor:N2}"));

                if (string.IsNullOrEmpty(msg))
                    msg = "Nenhuma despesa encontrada.";

                // Exibir em AlertDialog
                new AlertDialog.Builder(_context)
                    .SetTitle($"Despesas - {categoria}")
                    .SetMessage(msg)
                    .SetPositiveButton("OK", (s, ev) => { })
                    .Show();
            }
        }
    }
}
