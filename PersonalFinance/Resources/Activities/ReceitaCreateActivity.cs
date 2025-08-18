using Android.App;
using Android.OS;
using Android.Widget;
using PersonalFinance.Resources.ViewModels;
using System;
using System.Globalization;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Cadastro de Receita")]
    public class ReceitaCreateActivity : Activity
    {
        private EditText _edtMesReferencia;
        private EditText _edtFonte;
        private EditText _edtDescricao;
        private EditText _edtTipo;
        private EditText _edtValor;
        private Button _btnSalvar;

        private ReceitaViewModel _viewModel;
        private DateTime _mesSelecionado = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_receita_cadastro);

            _viewModel = new ReceitaViewModel();

            _edtMesReferencia = FindViewById<EditText>(Resource.Id.edtMesReferencia);
            _edtFonte = FindViewById<EditText>(Resource.Id.edtFonte);
            _edtDescricao = FindViewById<EditText>(Resource.Id.edtDescricao);
            _edtTipo = FindViewById<EditText>(Resource.Id.edtTipo);
            _edtValor = FindViewById<EditText>(Resource.Id.edtValor);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvar);

            // Preenche mês atual
            _edtMesReferencia.Text = _mesSelecionado.ToString("MM/yyyy");

            // Abrir DatePicker ao clicar no campo
            _edtMesReferencia.Click += (s, e) =>
            {
                var dialog = new DatePickerDialog(this,
                    (sender, args) =>
                    {
                        // Pega apenas mês e ano
                        _mesSelecionado = new DateTime(args.Date.Year, args.Date.Month, 1);
                        _edtMesReferencia.Text = _mesSelecionado.ToString("MM/yyyy");
                    },
                    _mesSelecionado.Year,
                    _mesSelecionado.Month - 1,
                    1); // dia fixo 1

                dialog.Show();
            };


            _btnSalvar.Click += async (s, e) =>
            {
                // Valida campos obrigatórios
                if (string.IsNullOrWhiteSpace(_edtFonte.Text))
                {
                    Toast.MakeText(this, "Informe a fonte pagadora.", ToastLength.Short).Show();
                    return;
                }

                if (string.IsNullOrWhiteSpace(_edtValor.Text))
                {
                    Toast.MakeText(this, "Informe o valor.", ToastLength.Short).Show();
                    return;
                }

                // Preenche ViewModel
                _viewModel.MesReferencia = _mesSelecionado;
                _viewModel.FontePagadora = _edtFonte.Text.Trim();
                _viewModel.Descricao = _edtDescricao.Text?.Trim();
                _viewModel.Tipo = _edtTipo.Text?.Trim();

                string textoValor = _edtValor.Text.Replace(".", ",");
                _viewModel.Valor = decimal.TryParse(textoValor, NumberStyles.Number, new CultureInfo("pt-BR"), out decimal val) ? val : 0;

                // Salvar
                bool sucesso = await _viewModel.SalvarReceita();
                if (sucesso)
                {
                    Toast.MakeText(this, "Receita cadastrada!", ToastLength.Short).Show();
                    Finish(); // Volta para a lista de receitas
                }
                else
                {
                    Toast.MakeText(this, "Erro ao cadastrar receita.", ToastLength.Short).Show();
                }
            };
        }
    }
}
