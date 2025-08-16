using PersonalFinance.Resources.Adapters;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Lista de Receitas")]
    public class ReceitaListActivity : Activity
    {
        private ListView _listView;
        private DatabaseService _db;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_receita_list);

            _listView = FindViewById<ListView>(Resource.Id.listReceitas);

            _db = new DatabaseService();

            // Carregar receitas do banco
            var lista = await _db.ListaReceitasAsync();

            // Usar o adapter customizado em vez de ArrayAdapter<string>
            var adapter = new ReceitaAdapter(this, lista);
            _listView.Adapter = adapter;
        }
    }
}
