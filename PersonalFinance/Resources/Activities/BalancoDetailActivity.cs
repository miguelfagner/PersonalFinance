using PersonalFinance.Resources.Adapters;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Balanço Mensal")]
    public class BalancoDetailActivity : Activity
    {
        private TextView tvTotalReceita, tvTotalDespesa, tvSaldo;
        private ListView lvDespesas;

        private DatabaseService _db;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_balanco);

            tvTotalReceita = FindViewById<TextView>(Resource.Id.tvTotalReceita);
            tvTotalDespesa = FindViewById<TextView>(Resource.Id.tvTotalDespesa);
            tvSaldo = FindViewById<TextView>(Resource.Id.tvSaldo);
            lvDespesas = FindViewById<ListView>(Resource.Id.lvDespesas);

            _db = new DatabaseService();

            await CarregarDadosAsync();
        }

        private async Task CarregarDadosAsync()
        {
            var despesas = await _db.ListaDespesasAsync();
            var receitas = await _db.ListaReceitasAsync();

            // Calcula totais do mês atual
            var hoje = DateTime.Today;
            var mes = hoje.Month;
            var ano = hoje.Year;

            var totalReceita = receitas
                .Where(r => r.MesReferencia.Month == mes && r.MesReferencia.Year == ano)
                .Sum(r => r.Valor);

            var totalDespesa = despesas
                .Where(d => d.Vencimento.Month == mes && d.Vencimento.Year == ano)
                .Sum(d => d.Valor);

            var saldo = totalReceita - totalDespesa;

            tvTotalReceita.Text = $"Receitas: R$ {totalReceita:N2}";
            tvTotalDespesa.Text = $"Despesas: R$ {totalDespesa:N2}";
            tvSaldo.Text = $"Saldo: R$ {saldo:N2}";

            // Carrega lista de despesas com seu adapter
            var adapter = new DespesaAdapter(this, despesas);
            lvDespesas.Adapter = adapter;
        }
    }
}
