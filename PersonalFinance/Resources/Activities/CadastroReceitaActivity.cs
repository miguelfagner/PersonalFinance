using PersonalFinance.Resources.ViewModels;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Cadastro de Receita")]
    public class CadastroReceitaActivity : Activity
    {
        private EditText _edtFonte, _edtDescricao, _edtTipo, _edtValor;
        private Button _btnSalvar;
        private ReceitaViewModel _viewModel;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.CadastroReceita);

            _viewModel = new ReceitaViewModel();

            _edtFonte = FindViewById<EditText>(Resource.Id.edtFonte);
            _edtValor = FindViewById<EditText>(Resource.Id.edtValor);
            _edtDescricao = FindViewById<EditText>(Resource.Id.edtDescricao);
            _edtTipo = FindViewById<EditText>(Resource.Id.edtTipo);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvar);

            _btnSalvar.Click += async (s, e) =>
            {
                _viewModel.FontePagadora = _edtFonte.Text;
                _viewModel.Descricao = _edtDescricao.Text;
                _viewModel.Tipo = _edtTipo.Text;
                _viewModel.Valor = decimal.TryParse(_edtValor.Text, out decimal val) ? val : 0;

                bool sucesso = await _viewModel.SalvarReceita();
                if (sucesso)
                    Toast.MakeText(this, "Receita cadastrada!", ToastLength.Short).Show();
                else
                    Toast.MakeText(this, "Informe a fonte pagadora.", ToastLength.Short).Show();

                _edtFonte.Text = "";
                _edtDescricao.Text = "";
                _edtTipo.Text = "";
                _edtValor.Text = "";
            };
        }
    }
}
