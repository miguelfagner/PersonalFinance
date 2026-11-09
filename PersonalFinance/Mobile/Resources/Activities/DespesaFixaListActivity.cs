using Android.Content;
using Android.Views;
using PersonalFinance.Resources.Adapters;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Despesas Fixas")]
    public class DespesaFixaListActivity : FinanceActivity
    {
        protected override Task RefreshAfterSyncAsync() => CarregarAsync();

        private FinanceRepository _db = null!;
        private ListView _lista = null!;
        private TextView _vazio = null!;
        private List<DespesaFixa> _itens = new();

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_despesa_fixa_list);

            _db = new FinanceRepository();
            _lista = FindViewById<ListView>(Resource.Id.listDespesasFixas)!;
            _vazio = FindViewById<TextView>(Resource.Id.tvDespesasFixasVazio)!;

            _lista.ItemClick += (s, e) =>
            {
                if (e.Position < 0 || e.Position >= _itens.Count)
                    return;

                var intent = new Intent(this, typeof(DespesaFixaEditActivity));
                intent.PutExtra(DespesaFixaEditActivity.ExtraDespesaFixaId, _itens[e.Position].Id);
                StartActivity(intent);
            };

            FindViewById<Button>(Resource.Id.btnAddDespesaFixa)!.Click += (s, e) =>
                StartActivity(new Intent(this, typeof(DespesaFixaCreateActivity)));
        }

        protected override async void OnResume()
        {
            base.OnResume();
            await CarregarAsync();
        }

        private async Task CarregarAsync()
        {
            try
            {
                var despesasTask = _db.ListarDespesasFixasAsync(incluirInativas: true);
                var receitasTask = _db.ListarReceitasFixasAsync(incluirInativas: true);
                await Task.WhenAll(despesasTask, receitasTask);

                _itens = despesasTask.Result
                    .OrderByDescending(x => x.Ativa)
                    .ThenBy(x => x.DiaVencimento)
                    .ThenBy(x => x.Descricao)
                    .ToList();

                var fontes = receitasTask.Result
                    .GroupBy(x => x.Id)
                    .ToDictionary(x => x.Key, x => x.First().FontePagadora);

                _lista.Adapter = new DespesaFixaAdapter(this, _itens, fontes);
                _vazio.Visibility = _itens.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
                _lista.Visibility = _itens.Count == 0 ? ViewStates.Gone : ViewStates.Visible;
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Não foi possível carregar as despesas fixas: {ex.Message}", ToastLength.Long)!.Show();
            }
        }
    }
}
