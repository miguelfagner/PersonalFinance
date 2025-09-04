using PersonalFinance.Resources.Adapters;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Balanço Mensal")]
    public class BalancoDetailActivity : Activity
    {
        private TextView tvTotalReceita, tvTotalDespesa, tvSaldo, tvTotalQuitado, tvFaltaQuitar;
        private ListView lvDespesas;

        private DatabaseService _db;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_balanco);

            tvTotalReceita = FindViewById<TextView>(Resource.Id.tvTotalReceita);
            tvTotalDespesa = FindViewById<TextView>(Resource.Id.tvTotalDespesa);
            tvTotalQuitado = FindViewById<TextView>(Resource.Id.tvTotalQuitado);
            tvSaldo = FindViewById<TextView>(Resource.Id.tvSaldo);
            tvFaltaQuitar = FindViewById<TextView>(Resource.Id.tvFaltaQuitar);
            lvDespesas = FindViewById<ListView>(Resource.Id.lvDespesas);

            _db = new DatabaseService();

            await CarregarDadosAsync();
        }

        private async Task CarregarDadosAsync()
        {
            var despesas = await _db.ListaDespesasAsync() ?? new List<Despesa>();
            var receitas = await _db.ListaReceitasAsync() ?? new List<Receita>();
            var quitado = await _db.ListaTransacoesAsync() ?? new List<Transacao>();

            // Calcula totais do mês atual
            var hoje = DateTime.Today;
            var mes = hoje.Month;
            var ano = hoje.Year;

            decimal totalReceita = receitas
                .Where(r => r?.MesReferencia != null && r.MesReferencia.Month == mes && r.MesReferencia.Year == ano)
                .Sum(r => r?.Valor ?? 0);

            decimal totalDespesa = despesas
                .Where(d => d?.Vencimento != null && d.Vencimento.Month == mes && d.Vencimento.Year == ano)
                .Sum(d => d?.Valor ?? 0);

            decimal totalQuitado = quitado
                .Where(r => r?.Data != null && r.Data.Month == mes && r.Data.Year == ano)
                .Sum(r => r?.Valor ?? 0);

            var saldo = totalReceita - totalDespesa;
            var faltaQuitar = totalDespesa - totalQuitado;

            // Usa operador null-conditional para evitar NullReference
            tvTotalReceita?.SetText($"Receitas: R$ {totalReceita:N2}", TextView.BufferType.Normal);
            tvTotalDespesa?.SetText($"Despesas: R$ {totalDespesa:N2}", TextView.BufferType.Normal);
            tvTotalQuitado?.SetText($"Quitado: R$ {totalQuitado:N2}", TextView.BufferType.Normal);
            tvFaltaQuitar?.SetText($"Falta quitar: R$ {faltaQuitar:N2}", TextView.BufferType.Normal);
            tvSaldo?.SetText($"Saldo: R$ {saldo:N2}", TextView.BufferType.Normal);

            // Carrega lista de despesas com adapter
            if (despesas.Any())
            {
                var adapter = new DespesaAdapter(this, despesas);
                lvDespesas.Adapter = adapter;
            }
        }
    }
}
