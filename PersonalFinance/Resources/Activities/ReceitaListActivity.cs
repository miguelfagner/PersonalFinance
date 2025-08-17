using Android.Content;
using PersonalFinance.Resources.Adapters;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Lista de Receitas")]
    public class ReceitaListActivity : Activity
    {
        private ListView _listView;
        private DatabaseService _db;
        private ReceitaAdapter _adapter;
        private List<Receita> _receitas;
        private Button _addReceita;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_receita_list);

            _listView = FindViewById<ListView>(Resource.Id.listReceitas);
            _addReceita = FindViewById<Button>(Resource.Id.btnAddReceita);


            _db = new DatabaseService();

            // Clique no item da lista → abre edição
            _listView.ItemClick += (s, e) =>
            {
                var receitaSelecionada = _receitas[e.Position];
                var intent = new Intent(this, typeof(ReceitaEditActivity));
                intent.PutExtra("ReceitaId", receitaSelecionada.Id);
                StartActivity(intent);
            };

            _addReceita.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(ReceitaCreateActivity));
                intent.PutExtra("tipo", "Receita");
                StartActivity(intent);
            };

            // Carrega na primeira vez
            _ = CarregarReceitasAsync();
        }

        protected override void OnResume()
        {
            base.OnResume();
            // Sempre que voltar, recarrega a lista
            _ = CarregarReceitasAsync();
        }

        private async Task CarregarReceitasAsync()
        {
            _receitas = await _db.ListaReceitasAsync();

            if (_adapter == null)
            {
                _adapter = new ReceitaAdapter(this, _receitas);
                _listView.Adapter = _adapter;
            }
            else
            {
                _adapter = new ReceitaAdapter(this, _receitas);
                _listView.Adapter = _adapter;
            }
        }
    }
}
