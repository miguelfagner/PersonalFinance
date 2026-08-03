using Android.Content;
using Android.Views;
using PersonalFinance.Resources.Adapters;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Receitas Fixas")]
    public class ReceitaFixaListActivity : Activity
    {
        private DatabaseService _db = null!;
        private ListView _lista = null!;
        private TextView _vazio = null!;
        private List<ReceitaFixa> _itens = new();

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_receita_fixa_list);

            _db = new DatabaseService();
            _lista = FindViewById<ListView>(Resource.Id.listReceitasFixas)!;
            _vazio = FindViewById<TextView>(Resource.Id.tvReceitasFixasVazio)!;

            _lista.ItemClick += (s, e) =>
            {
                if (e.Position < 0 || e.Position >= _itens.Count)
                    return;

                var intent = new Intent(this, typeof(ReceitaFixaEditActivity));
                intent.PutExtra(ReceitaFixaEditActivity.ExtraReceitaFixaId, _itens[e.Position].Id);
                StartActivity(intent);
            };

            FindViewById<Button>(Resource.Id.btnAddReceitaFixa)!.Click += (s, e) =>
                StartActivity(new Intent(this, typeof(ReceitaFixaCreateActivity)));
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
                _itens = (await _db.ListarReceitasFixasAsync(incluirInativas: true))
                    .OrderByDescending(x => x.Ativa)
                    .ThenBy(x => x.FontePagadora)
                    .ThenBy(x => x.Descricao)
                    .ToList();

                _lista.Adapter = new ReceitaFixaAdapter(this, _itens);
                _vazio.Visibility = _itens.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
                _lista.Visibility = _itens.Count == 0 ? ViewStates.Gone : ViewStates.Visible;
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Não foi possível carregar as receitas fixas: {ex.Message}", ToastLength.Long)!.Show();
            }
        }
    }
}
