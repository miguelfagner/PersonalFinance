using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Cadastro")]
    public class CadastroActivity : Activity
    {
        //private EditText _descricao, _valor;
        EditText _txtDescricao, _txtValor;

        private Button _btnSalvar;
        Spinner _spinnerTipo;
        private DatabaseService _db;
        //private string _tipo;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_cadastro);

            _txtDescricao = FindViewById<EditText>(Resource.Id.editDescricao)                ;
            _spinnerTipo = FindViewById<Spinner>(Resource.Id.spinnerTipo);
            _txtValor = FindViewById<EditText>(Resource.Id.editValor);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvar);

            int _idTransacao = 0; // Guardar ID para saber se é edição

            _db = new DatabaseService();
            //_tipo = Intent.GetStringExtra("tipo");

            // Verifica se veio um Id na Intent (edição)
            _idTransacao = Intent.GetIntExtra("Id", 0);
            if (_idTransacao != 0)
            {
                var transacao = (await _db.GetTransacoesAsync()).FirstOrDefault(t => t.Id == _idTransacao);

                if (transacao != null)
                {
                    _txtDescricao.Text = transacao.Descricao;
                    _txtValor.Text = transacao.Valor.ToString("F2");
                    var indexTipo = transacao.Tipo == "Receita" ? 0 : 1;
                    _spinnerTipo.SetSelection(indexTipo);
                }
            }

            _btnSalvar.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtDescricao.Text) || string.IsNullOrWhiteSpace(_txtValor.Text))
                {
                    Toast.MakeText(this, "Preencha todos os campos!", ToastLength.Short).Show();
                    return;
                }

                new AlertDialog.Builder(this)
                    .SetTitle("Confirmação")
                    //.SetMessage($"Deseja salvar esta {_tipo.ToLower()}?")
                    .SetMessage($"Deseja salvar ?")
                    .SetPositiveButton("Sim", async (senderAlert, args) =>
                    {
                        var transacao = new Transacao
                        {
                            Id = _idTransacao,
                            Tipo = "Receita",
                            Descricao = _txtDescricao.Text,
                            Valor = decimal.Parse(_txtValor.Text),
                            Data = DateTime.Now,
                            Pago = true //_tipo == "Receita" // Receitas já como pagas
                        };
                        await _db.SalvarTransacaoAsync(transacao);
                        //Toast.MakeText(this, $"{_tipo} salva!", ToastLength.Short).Show();
                        Toast.MakeText(this, $"salvo!", ToastLength.Short).Show();
                        Finish();
                    })
                    .SetNegativeButton("Não", (senderAlert, args) => { })
                    .Show();
            };
        }
    }
}
