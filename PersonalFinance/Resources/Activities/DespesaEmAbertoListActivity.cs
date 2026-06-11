using Android.Content;
using Android.Views;
using PersonalFinance.Resources.Adapters;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.Globalization;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Valores a quitar")]
    public class DespesaEmAbertoListActivity : Activity
    {
        private static readonly CultureInfo PtBr = new("pt-BR");
        private DatabaseService _db;
        private List<DespesaEmAberto> _despesas;
        private ListView _listView;
        private TextView _tvTotalRestante;
        private TextView _tvResumoPendencias;
        private TextView _tvSemPendencias;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_despesa_em_aberto_list);

            _db = new DatabaseService();
            _listView = FindViewById<ListView>(Resource.Id.listDespesasEmAberto)!;
            _tvTotalRestante = FindViewById<TextView>(Resource.Id.tvTotalRestante)!;
            _tvResumoPendencias = FindViewById<TextView>(Resource.Id.tvResumoPendencias)!;
            _tvSemPendencias = FindViewById<TextView>(Resource.Id.tvSemPendencias)!;

            _listView.ItemClick += (s, e) =>
            {
                var intent = new Intent(this, typeof(DespesaEditActivity));
                intent.PutExtra("DespesaId", _despesas[e.Position].Despesa.Id);
                StartActivity(intent);
            };
        }

        protected override async void OnResume()
        {
            base.OnResume();
            await CarregarDespesasAsync();
        }

        private async Task CarregarDespesasAsync()
        {
            var inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var fimMes = inicioMes.AddMonths(1).AddTicks(-1);

            _despesas = await _db.ListaDespesasEmAbertoAsync(inicioMes, fimMes);
            var totalRestante = _despesas.Sum(x => x.ValorRestante);
            var vencidas = _despesas.Count(x => x.Despesa.Vencimento.Date < DateTime.Today);
            var nomeMes = inicioMes.ToString("MMMM 'de' yyyy", PtBr);
            var textoDespesas = _despesas.Count == 1
                ? "1 despesa em aberto"
                : $"{_despesas.Count} despesas em aberto";
            var textoVencidas = vencidas == 1
                ? "1 vencida"
                : $"{vencidas} vencidas";

            _tvTotalRestante.Text = $"R$ {totalRestante.ToString("N2", PtBr)}";
            _tvResumoPendencias.Text = $"{textoDespesas} - {textoVencidas} - {nomeMes}";
            _tvSemPendencias.Text = $"Nenhuma despesa em aberto em {nomeMes}.";

            _tvSemPendencias.Visibility = _despesas.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
            _listView.Visibility = _despesas.Count == 0 ? ViewStates.Gone : ViewStates.Visible;
            _listView.Adapter = new DespesaEmAbertoAdapter(this, _despesas);
        }
    }
}
