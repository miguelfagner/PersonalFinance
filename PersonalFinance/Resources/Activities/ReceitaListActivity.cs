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

            // Caminho do banco
            _db = new DatabaseService();

            var lista = await _db.GetReceitasAsync();

            // Transformar em lista de strings para mostrar no ListView
            var displayList = lista
                .OrderByDescending(r => r.MesReferencia) // mostrar do mês mais recente
                .Select(r => $"{r.MesReferencia:MM/yyyy} | {r.FontePagadora} | {r.Descricao} | {r.Valor:C}")
                .ToList();

            _listView.Adapter = new ArrayAdapter<string>(
                this,
                Android.Resource.Layout.SimpleListItem1,
                displayList
            );
        }
    }
}
