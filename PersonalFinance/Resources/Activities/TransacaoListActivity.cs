using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Transações")]
    public class TransacaoListActivity : Activity
    {
        private ListView _listView;
        private Button _btnNova;
        private DatabaseService _db;
        private List<Transacao> _transacoes;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (ActionBar != null)
                ActionBar.Hide();

            SetContentView(Resource.Layout.activity_transacao_list);

            _db = new DatabaseService();

            // Vincular componentes
            _listView = FindViewById<ListView>(Resource.Id.listViewTransacoes);
            _btnNova = FindViewById<Button>(Resource.Id.btnNovaTransacao);

            // Carregar transações
            await CarregarTransacoes();

            // Clique em item da lista → abrir edição
            _listView.ItemClick += (s, e) =>
            {
                var transacao = _transacoes[e.Position];
                var intent = new Intent(this, typeof(TransacaoEditActivity));
                intent.PutExtra("TransacaoId", transacao.Id);
                StartActivity(intent);
            };

            // Clique no botão nova transação
            _btnNova.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(TransacaoEditActivity));
                StartActivity(intent);
            };
        }

        protected override async void OnResume()
        {
            base.OnResume();
            await CarregarTransacoes();
        }

        private async Task CarregarTransacoes()
        {
            _transacoes = await _db.ListaTransacoesAsync();

            var adapter = new ArrayAdapter<string>(
                this,
                Android.Resource.Layout.SimpleListItem1,
                _transacoes.Select(t => $"{t.Data:dd/MM/yyyy} - R$ {t.Valor:F2} - {t.Observacao}").ToList()
            );

            _listView.Adapter = adapter;
        }
    }
}
