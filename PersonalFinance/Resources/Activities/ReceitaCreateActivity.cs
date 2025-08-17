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

            _btnSalvar.Click += async (s, e) =>
            {
                if (_viewModel.MesReferencia == default)
                    _viewModel.MesReferencia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

                _viewModel.FontePagadora = _edtFonte.Text;
                _viewModel.Descricao = _edtDescricao.Text;
                _viewModel.Tipo = _edtTipo.Text;

                // 🔑 Aceita ponto OU vírgula
                string textoValor = _edtValor.Text.Replace(".", ",");
                _viewModel.Valor = decimal.TryParse(textoValor, NumberStyles.Number, new CultureInfo("pt-BR"), out decimal val) ? val : 0;

                bool sucesso = await _viewModel.SalvarReceita();
                if (sucesso)
                    Toast.MakeText(this, "Receita cadastrada!", ToastLength.Short).Show();
                else
                    Toast.MakeText(this, "Informe a fonte pagadora.", ToastLength.Short).Show();

                // Limpa os campos
                _edtMesReferencia.Text = "";
                _edtFonte.Text = "";
                _edtDescricao.Text = "";
                _edtTipo.Text = "";
                _edtValor.Text = "";

                _viewModel.MesReferencia = default;
            };
        }
    }
}
