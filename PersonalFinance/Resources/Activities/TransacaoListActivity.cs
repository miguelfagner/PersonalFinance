using Android.Content;
using PersonalFinance.Resources.Adapters;
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
        private TransacaoAdapter _adapter;

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

            await CarregarTransacoes();

            // Clique em item → editar
            _listView.ItemClick += (s, e) =>
            {
                var transacao = _transacoes[e.Position];
                var intent = new Intent(this, typeof(TransacaoEditActivity));
                intent.PutExtra("TransacaoId", transacao.Id);
                StartActivity(intent);
            };

            // Nova transação
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
            // ListaTransacoesAsync já carrega Despesa e Receita
            _transacoes = await _db.ListaTransacoesAsync();

            _adapter = new TransacaoAdapter(this, _transacoes);
            _listView.Adapter = _adapter;
        }
    }
}
