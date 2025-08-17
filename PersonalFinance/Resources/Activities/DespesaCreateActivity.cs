using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using PersonalFinance.Resources.ViewModels;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Cadastro de Despesa")]
    public class DespesaCreateActivity : Activity
    {
        private Spinner _spinnerReceita;
        private EditText _edtDescricao, _edtCategoria, _edtValor, _edtNParcela;
        private Button _btnSalvar;
        private DespesaViewModel _viewModel;
        private List<Receita> _receitas;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_despesa_cadastro);

            _viewModel = new DespesaViewModel();

            _spinnerReceita = FindViewById<Spinner>(Resource.Id.spinnerReceita);
            _edtDescricao = FindViewById<EditText>(Resource.Id.edtDescricao);
            _edtCategoria = FindViewById<EditText>(Resource.Id.edtCategoria);
            _edtValor = FindViewById<EditText>(Resource.Id.edtValor);
            _edtNParcela = FindViewById<EditText>(Resource.Id.edtNParcela);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvar);

            // Carregar receitas do banco
            var db = new DatabaseService();
            _receitas = await db.ListaReceitasAsync();

            // Adapter do Spinner
            var adapter = new ArrayAdapter(this, Android.Resource.Layout.SimpleSpinnerItem, _receitas.Select(r => r.FontePagadora).ToList());
            adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            _spinnerReceita.Adapter = adapter;

            _btnSalvar.Click += async (s, e) =>
            {
                var selectedIndex = _spinnerReceita.SelectedItemPosition;
                if (selectedIndex < 0 || selectedIndex >= _receitas.Count)
                {
                    Toast.MakeText(this, "Selecione uma receita.", ToastLength.Short).Show();
                    return;
                }

                _viewModel.ReceitaId = _receitas[selectedIndex].Id;
                _viewModel.Descricao = _edtDescricao.Text;
                _viewModel.Categoria = _edtCategoria.Text;
                _viewModel.Valor = decimal.TryParse(_edtValor.Text, out decimal val) ? val : 0;
                _viewModel.NParcela = int.TryParse(_edtNParcela.Text, out int parcela) ? parcela : 0;

                bool sucesso = await _viewModel.SalvarDespesa();
                if (sucesso)
                    Toast.MakeText(this, "Despesa cadastrada!", ToastLength.Short).Show();
                else
                    Toast.MakeText(this, "Erro ao cadastrar despesa.", ToastLength.Short).Show();

                _edtDescricao.Text = "";
                _edtCategoria.Text = "";
                _edtValor.Text = "";
                _edtNParcela.Text = "";
            };
        }
    }
}
