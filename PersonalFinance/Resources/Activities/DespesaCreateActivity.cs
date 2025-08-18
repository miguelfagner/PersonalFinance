using Android.App;
using Android.OS;
using Android.Widget;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using PersonalFinance.Resources.ViewModels;
using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Cadastro de Despesa")]
    public class DespesaCreateActivity : Activity
    {
        private Spinner _spinnerReceita;
        private EditText _edtDescricao, _edtCategoria, _edtValor, _edtNParcela, _edtData;
        private Button _btnSalvar;
        private DespesaViewModel _viewModel;
        private List<Receita> _receitas;
        private DateTime _dataSelecionada = DateTime.Today;

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
            _edtData = FindViewById<EditText>(Resource.Id.edtVencimento);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvar);

            // Carregar receitas do banco
            var db = new DatabaseService();
            _receitas = await db.ListaReceitasAsync();

            // Adapter do Spinner
            var adapter = new ArrayAdapter(this, Android.Resource.Layout.SimpleSpinnerItem,
                _receitas.Select(r => r.FontePagadora).ToList());
            adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            _spinnerReceita.Adapter = adapter;

            // Mostrar a data de hoje como padrão
            _edtData.Text = _dataSelecionada.ToString("dd/MM/yyyy");

            // Abrir o DatePicker ao clicar no campo
            _edtData.Click += (s, e) =>
            {
                var dialog = new DatePickerDialog(this,
                    (sender, args) =>
                    {
                        _dataSelecionada = args.Date;
                        _edtData.Text = _dataSelecionada.ToString("dd/MM/yyyy");
                    },
                    _dataSelecionada.Year,
                    _dataSelecionada.Month - 1,
                    _dataSelecionada.Day);

                dialog.Show();
            };

            // Salvar despesa
            _btnSalvar.Click += async (s, e) =>
            {
                var selectedIndex = _spinnerReceita.SelectedItemPosition;
                if (selectedIndex < 0 || selectedIndex >= _receitas.Count)
                {
                    Toast.MakeText(this, "Selecione uma receita.", ToastLength.Short).Show();
                    return;
                }

                _viewModel.ReceitaId = _receitas[selectedIndex].Id;
                _viewModel.Descricao = _edtDescricao.Text?.Trim();
                _viewModel.Categoria = _edtCategoria.Text?.Trim();
                string valorTexto = _edtValor.Text?.Replace(",", ".") ?? "0";
                _viewModel.Valor = decimal.TryParse(valorTexto, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val) ? val : 0;
                _viewModel.NParcela = int.TryParse(_edtNParcela.Text, out int parcela) ? parcela : 0;
                _viewModel.Vencimento = _dataSelecionada;

                bool sucesso = await _viewModel.SalvarDespesa();
                if (sucesso)
                {
                    Toast.MakeText(this, "Despesa cadastrada!", ToastLength.Short).Show();
                    // Fecha a Activity e volta para a lista atualizada
                    Finish();
                }
                else
                {
                    Toast.MakeText(this, "Erro ao cadastrar despesa.", ToastLength.Short).Show();
                }
            };

            // Corrigir vírgula no campo de valor em tempo real
            _edtValor.TextChanged += (s, e) =>
            {
                if (_edtValor.Text.Contains("."))
                {
                    _edtValor.Text = _edtValor.Text.Replace(".", ",");
                    _edtValor.SetSelection(_edtValor.Text.Length);
                }
            };
        }

        //private void LimparCampos()
        //{
        //    _edtDescricao.Text = "";
        //    _edtCategoria.Text = "";
        //    _edtValor.Text = "";
        //    _edtNParcela.Text = "";
        //    _edtData.Text = DateTime.Today.ToString("dd/MM/yyyy");
        //    _spinnerReceita.SetSelection(0);
        //    _dataSelecionada = DateTime.Today;
        //}
    }
}
