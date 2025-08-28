using Android.Content;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Transacoes")]
    public class TransacaoDetailActivity : Activity
    {
        private DatabaseService _db;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_transacao_detail);

            // Receber a transacao enviada pela lista
            var transacaoId = Intent.GetIntExtra("TransacaoId", -1);

            if (transacaoId == -1)
            {
                Toast.MakeText(this, "Transacao inválida", ToastLength.Short).Show();
                Finish();
                return;
            }

            // Buscar transacao no banco
            _db = new DatabaseService();
            var transacao = await _db.PegarTransacaoAsync(transacaoId);
            if (transacao == null)
            {
                Toast.MakeText(this, "Transacao não encontrada", ToastLength.Short).Show();
                Finish();
                return;
            }

            // Preencher campos
            //FindViewById<TextView>(Resource.Id.tvDescricao).Text = transacao.Descricao;
            //FindViewById<TextView>(Resource.Id.tvCategoria).Text = transacao.Categoria;
            //FindViewById<TextView>(Resource.Id.tvValor).Text = $"R$ {transacao.Valor:N2}";
            //FindViewById<TextView>(Resource.Id.tvDataCadastro).Text = transacao.DataCadastro.ToString("dd/MM/yyyy");
            //FindViewById<TextView>(Resource.Id.tvVencimento).Text = transacao.Vencimento.ToString("dd/MM/yyyy");
            //FindViewById<TextView>(Resource.Id.tvNParcela).Text = transacao.NParcela.ToString();
            //FindViewById<TextView>(Resource.Id.tvReceitaId).Text = transacao.ReceitaId.ToString();
        }
    }
}
