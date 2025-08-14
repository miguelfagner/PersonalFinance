using PersonalFinance.Resources.ViewModels;
using Android.App;
using Android.OS;
using Android.Widget;
using Java.Util;
using System;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Cadastro de Receita")]
    public class CadastroReceitaActivity : Activity
    {
        private EditText _edtMesReferencia, _edtFonte, _edtDescricao, _edtTipo, _edtValor;
        private Button _btnSalvar;
        private ReceitaViewModel _viewModel;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.CadastroReceita);

            _viewModel = new ReceitaViewModel();

            // Campos
            _edtMesReferencia = FindViewById<EditText>(Resource.Id.edtMesReferencia);
            _edtFonte = FindViewById<EditText>(Resource.Id.edtFonte);
            _edtValor = FindViewById<EditText>(Resource.Id.edtValor);
            _edtDescricao = FindViewById<EditText>(Resource.Id.edtDescricao);
            _edtTipo = FindViewById<EditText>(Resource.Id.edtTipo);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvar);

            // Abre DatePickerDialog ao clicar no campo de Mês de Referência
            _edtMesReferencia.Click += (s, e) =>
            {
                var hoje = DateTime.Today;
                var datePicker = new DatePickerDialog(this, (sender, ev) =>
                {
                    // Ajusta para o primeiro dia do mês selecionado
                    var selecionada = new DateTime(ev.Date.Year, ev.Date.Month, 1);
                    _edtMesReferencia.Text = selecionada.ToString("MM/yyyy");
                    _viewModel.MesReferencia = selecionada;
                },
                hoje.Year, hoje.Month - 1, hoje.Day);

                // Esconde seleção de dia, deixando só mês/ano (não é suportado nativamente, mas setamos dia como 1)
                datePicker.DatePicker.CalendarViewShown = false;
                datePicker.Show();
            };

            _btnSalvar.Click += async (s, e) =>
            {
                // Se não selecionou, usa mês atual como padrão
                if (_viewModel.MesReferencia == default)
                    _viewModel.MesReferencia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

                _viewModel.FontePagadora = _edtFonte.Text;
                _viewModel.Descricao = _edtDescricao.Text;
                _viewModel.Tipo = _edtTipo.Text;
                _viewModel.Valor = decimal.TryParse(_edtValor.Text, out decimal val) ? val : 0;

                bool sucesso = await _viewModel.SalvarReceita();
                if (sucesso)
                    Toast.MakeText(this, "Receita cadastrada!", ToastLength.Short).Show();
                else
                    Toast.MakeText(this, "Informe a fonte pagadora.", ToastLength.Short).Show();

                // Limpa campos
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
