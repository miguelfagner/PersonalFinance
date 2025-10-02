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
        private DespesaAdapter adapter;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_despesa_list);

            listView = FindViewById<ListView>(Resource.Id.listDespesas);
            btnAddDespesa = FindViewById<Button>(Resource.Id.btnAddDespesa);

            _db = new DatabaseService();

            // Carrega a lista inicial
            var mesRef = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            despesas = await _db.ListaDespesasAsync(mesRef);
            adapter = new DespesaAdapter(this, despesas);
            listView.Adapter = adapter;

            // Clique no item da lista abre detalhes
            listView.ItemClick += (s, e) =>
            {
                var despesa = despesas[e.Position];
                var intent = new Intent(this, typeof(DespesaEditActivity));
                intent.PutExtra("DespesaId", despesa.Id);
                StartActivity(intent);
            };

            btnAddDespesa.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(DespesaCreateActivity));
                StartActivity(intent);
            };
        }

        protected override async void OnResume()
        {
            base.OnResume();

            // Atualiza a lista sempre que voltar para a Activity
            var mesRef = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            despesas = await _db.ListaDespesasAsync(mesRef);

            // Atualiza o adapter
            adapter = new DespesaAdapter(this, despesas);
            listView.Adapter = adapter;
        }
    }
}
