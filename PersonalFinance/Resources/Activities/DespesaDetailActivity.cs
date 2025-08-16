using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Detalhes da Despesa")]
    public class DespesaDetailActivity : Activity
    {
        private DatabaseService _db;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_despesa_detail);

            // Receber a despesa enviada pela lista
            var despesaId = Intent.GetIntExtra("DespesaId", -1);

            if (despesaId == -1)
            {
                Toast.MakeText(this, "Despesa inválida", ToastLength.Short).Show();
                Finish();
                return;
            }

            // Buscar despesa no banco
            _db = new DatabaseService();
            var despesa = await _db.PegarDespesaAsync(despesaId);
            if (despesa == null)
            {
                Toast.MakeText(this, "Despesa não encontrada", ToastLength.Short).Show();
                Finish();
                return;
            }

            // Preencher campos
            FindViewById<TextView>(Resource.Id.tvDescricao).Text = despesa.Descricao;
            FindViewById<TextView>(Resource.Id.tvCategoria).Text = despesa.Categoria;
            FindViewById<TextView>(Resource.Id.tvValor).Text = $"R$ {despesa.Valor:N2}";
            FindViewById<TextView>(Resource.Id.tvDataCadastro).Text = despesa.DataCadastro.ToString("dd/MM/yyyy");
            FindViewById<TextView>(Resource.Id.tvVencimento).Text = despesa.Vencimento.ToString("dd/MM/yyyy");
            FindViewById<TextView>(Resource.Id.tvNParcela).Text = despesa.NParcela.ToString();
            FindViewById<TextView>(Resource.Id.tvReceitaId).Text = despesa.ReceitaId.ToString();
        }
    }
}
