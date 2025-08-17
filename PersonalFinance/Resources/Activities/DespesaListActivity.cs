using Android.Content;
using PersonalFinance.Resources.Adapters;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Despesas")]
    public class DespesaListActivity : Activity
    {
        ListView listView;
        Button btnAddDespesa;
        List<Despesa> despesas;
        private DatabaseService _db;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_despesa_list);

            listView = FindViewById<ListView>(Resource.Id.listDespesas);
            btnAddDespesa = FindViewById<Button>(Resource.Id.btnAddDespesa);

            _db = new DatabaseService();

            var lista = await _db.ListaDespesasAsync();
            despesas = lista;

            var adapter = new DespesaAdapter(this, despesas);
            listView.Adapter = adapter;

            // Clique no item da lista abre detalhes
            listView.ItemClick += (s, e) =>
            {
                var despesa = despesas[e.Position];
                var intent = new Intent(this, typeof(DespesaDetailActivity));
                intent.PutExtra("DespesaId", despesa.Id);
                StartActivity(intent);
            };

            btnAddDespesa.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(DespesaCreateActivity));
                //intent.PutExtra("tipo", "Despesa");
                StartActivity(intent);
            };
        }

        //private List<Despesa> CarregarDespesas()
        //{
        //    var dbPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "personalfinance.db");
        //    using var db = new SQLiteConnection(dbPath);
        //    db.CreateTable<Despesa>();
        //    return db.Table<Despesa>().ToList();
        //}
    }
}
