using Android.App;
using Android.OS;
using Android.Widget;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using PersonalFinance.Resources.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

            // Inicializa ViewModel
            _viewModel = new DespesaViewModel();

            // Vincular componentes
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

            // Receber dados passados via intent (se houver)
            var descricao = Intent.GetStringExtra("Descricao");
            var categoria = Intent.GetStringExtra("Categoria");
            var valor = Intent.GetDoubleExtra("Valor", 0);
            var nParcela = Intent.GetIntExtra("NParcela", 0);
            var ticks = Intent.GetLongExtra("Vencimento", 0);
            var receitaId = Intent.GetIntExtra("ReceitaId", 0);

            if (!string.IsNullOrEmpty(descricao)) _edtDescricao.Text = descricao;
            if (!string.IsNullOrEmpty(categoria)) _edtCategoria.Text = categoria;
            _edtValor.Text = valor.ToString("N2", CultureInfo.InvariantCulture);
            _edtNParcela.Text = nParcela.ToString();
            if (ticks > 0)
            {
                _dataSelecionada = new DateTime(ticks);
                _edtData.Text = _dataSelecionada.ToString("dd/MM/yyyy");
            }

            // Selecionar receita no spinner, se necessário
            if (receitaId > 0)
            {
                int index = _receitas.FindIndex(r => r.Id == receitaId);
                if (index >= 0) _spinnerReceita.SetSelection(index);
            }

            // Mostrar a data de hoje como padrão se não veio do intent
            if (_edtData.Text == "") _edtData.Text = _dataSelecionada.ToString("dd/MM/yyyy");

            // Abrir DatePicker ao clicar no campo de data
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

            // Corrigir vírgula no campo de valor em tempo real
            _edtValor.TextChanged += (s, e) =>
            {
                if (_edtValor.Text.Contains("."))
                {
                    _edtValor.Text = _edtValor.Text.Replace(".", ",");
                    _edtValor.SetSelection(_edtValor.Text.Length);
                }
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
                _viewModel.Descricao = _edtDescricao.Text?.Trim().ToUpper();
                _viewModel.Categoria = _edtCategoria.Text?.Trim().ToUpper();

                string valorTexto = _edtValor.Text?.Replace(",", ".") ?? "0";
                _viewModel.Valor = decimal.TryParse(valorTexto, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val) ? val : 0;

                _viewModel.NParcela = int.TryParse(_edtNParcela.Text, out int parcela) ? parcela : 0;
                _viewModel.Vencimento = _dataSelecionada;

                bool sucesso = await _viewModel.SalvarDespesa();
                if (sucesso)
                {
                    Toast.MakeText(this, "Despesa cadastrada!", ToastLength.Short).Show();
                    Finish(); // fecha Activity e volta para a lista
                }
                else
                {
                    Toast.MakeText(this, "Erro ao cadastrar despesa.", ToastLength.Short).Show();
                }
            };
        }
    }
}
