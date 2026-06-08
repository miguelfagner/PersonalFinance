using Android.Views;
using PersonalFinance.Resources.Helpers;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.Globalization;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Editar Transacao")]
    public class TransacaoEditActivity : Activity
    {
        private Spinner _spinnerReceita, _spinnerCategoria;
        private EditText _edtDescricaoDespesa, _edtVencimentoDespesa, _edtValorDespesa;
        private EditText _edtData, _edtValor, _edtObservacao;
        private Button _btnSalvar, _btnExcluir;

        private DatabaseService _db;
        private Transacao _transacao;
        private Despesa _despesa;
        private List<Receita> _receitas;
        private DateTime _vencimentoSelecionado;
        private DateTime _dataPagamentoSelecionada;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (ActionBar != null)
                ActionBar.Hide();

            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.LightStatusBar;
            SetContentView(Resource.Layout.activity_transacao_edit);

            _db = new DatabaseService();

            int transacaoId = Intent.GetIntExtra("TransacaoId", 0);
            if (transacaoId == 0)
            {
                Toast.MakeText(this, "Erro ao carregar transacao.", ToastLength.Short).Show();
                Finish();
                return;
            }

            _transacao = await _db.PegarTransacaoAsync(transacaoId);
            if (_transacao == null || _transacao.DespesaId == 0)
            {
                Toast.MakeText(this, "Transacao nao encontrada.", ToastLength.Short).Show();
                Finish();
                return;
            }

            _despesa = await _db.PegarDespesaAsync(_transacao.DespesaId);
            if (_despesa == null)
            {
                Toast.MakeText(this, "Despesa da transacao nao encontrada.", ToastLength.Short).Show();
                Finish();
                return;
            }

            _spinnerReceita = FindViewById<Spinner>(Resource.Id.spinnerReceita);
            _spinnerCategoria = FindViewById<Spinner>(Resource.Id.spinnerCategoria);
            _edtDescricaoDespesa = FindViewById<EditText>(Resource.Id.edtDescricaoDespesa);
            _edtVencimentoDespesa = FindViewById<EditText>(Resource.Id.edtVencimentoDespesa);
            _edtValorDespesa = FindViewById<EditText>(Resource.Id.edtValorDespesa);
            _edtData = FindViewById<EditText>(Resource.Id.edtData);
            _edtValor = FindViewById<EditText>(Resource.Id.edtValor);
            _edtObservacao = FindViewById<EditText>(Resource.Id.edtObservacao);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvarTransacao);
            _btnExcluir = FindViewById<Button>(Resource.Id.btnExcluirTransacao);

            await CarregarReceitasAsync();
            CarregarCategorias();
            PreencherCampos();
            ConfigurarSeletoresDeData();
            ConfigurarCamposDeValor();

            _btnSalvar.Click += async (s, e) => await SalvarAsync();
            _btnExcluir.Click += async (s, e) => await ConfirmarExclusaoAsync();
        }

        private async Task CarregarReceitasAsync()
        {
            _receitas = await _db.ListaReceitasAsync();
            _spinnerReceita.Adapter = FormOptions.CreateSpinnerAdapter(this, _receitas.Select(r => r.FontePagadora));

            int receitaIndex = _receitas.FindIndex(r => r.Id == _despesa.ReceitaId);
            if (receitaIndex >= 0) _spinnerReceita.SetSelection(receitaIndex);
        }

        private void CarregarCategorias()
        {
            var categorias = FormOptions.WithCurrentOption(FormOptions.CategoriasDespesa, _despesa.Categoria);
            _spinnerCategoria.Adapter = FormOptions.CreateSpinnerAdapter(this, categorias);

            int categoriaIndex = categorias.FindIndex(c => string.Equals(c, _despesa.Categoria, StringComparison.OrdinalIgnoreCase));
            if (categoriaIndex >= 0) _spinnerCategoria.SetSelection(categoriaIndex);
        }

        private void PreencherCampos()
        {
            _vencimentoSelecionado = _despesa.Vencimento == default ? DateTime.Today : _despesa.Vencimento;
            _dataPagamentoSelecionada = _transacao.Data == default ? DateTime.Today : _transacao.Data;

            _edtDescricaoDespesa.Text = _despesa.Descricao;
            _edtVencimentoDespesa.Text = _vencimentoSelecionado.ToString("dd/MM/yyyy");
            _edtValorDespesa.Text = _despesa.Valor.ToString("F2", new CultureInfo("pt-BR"));

            _edtData.Text = _dataPagamentoSelecionada.ToString("dd/MM/yyyy");
            _edtValor.Text = _transacao.Valor.ToString("F2", new CultureInfo("pt-BR"));
            _edtObservacao.Text = _transacao.Observacao;
        }

        private void ConfigurarSeletoresDeData()
        {
            _edtVencimentoDespesa.Click += (s, e) =>
            {
                var dialog = new DatePickerDialog(this, (sender, args) =>
                {
                    _vencimentoSelecionado = args.Date;
                    _edtVencimentoDespesa.Text = _vencimentoSelecionado.ToString("dd/MM/yyyy");
                }, _vencimentoSelecionado.Year, _vencimentoSelecionado.Month - 1, _vencimentoSelecionado.Day);

                dialog.Show();
            };

            _edtData.Click += (s, e) =>
            {
                var dialog = new DatePickerDialog(this, (sender, args) =>
                {
                    _dataPagamentoSelecionada = args.Date;
                    _edtData.Text = _dataPagamentoSelecionada.ToString("dd/MM/yyyy");
                }, _dataPagamentoSelecionada.Year, _dataPagamentoSelecionada.Month - 1, _dataPagamentoSelecionada.Day);

                dialog.Show();
            };
        }

        private void ConfigurarCamposDeValor()
        {
            _edtValorDespesa.TextChanged += (s, e) => NormalizarDecimal(_edtValorDespesa);
            _edtValor.TextChanged += (s, e) => NormalizarDecimal(_edtValor);
        }

        private static void NormalizarDecimal(EditText campo)
        {
            if (campo.Text.Contains("."))
            {
                campo.Text = campo.Text.Replace(".", ",");
                campo.SetSelection(campo.Text.Length);
            }
        }

        private async Task SalvarAsync()
        {
            try
            {
                var receitaIndex = _spinnerReceita.SelectedItemPosition;
                if (receitaIndex < 0 || receitaIndex >= _receitas.Count)
                {
                    Toast.MakeText(this, "Selecione uma receita.", ToastLength.Short).Show();
                    return;
                }

                _despesa.ReceitaId = _receitas[receitaIndex].Id;
                _despesa.Categoria = _spinnerCategoria.SelectedItem?.ToString();
                _despesa.Descricao = _edtDescricaoDespesa.Text?.Trim().ToUpperInvariant();
                _despesa.Vencimento = _vencimentoSelecionado;
                _despesa.Valor = ParseDecimal(_edtValorDespesa.Text);

                _transacao.Data = _dataPagamentoSelecionada;
                _transacao.Valor = ParseDecimal(_edtValor.Text);
                _transacao.Observacao = _edtObservacao.Text?.Trim();

                if (_despesa.Valor <= 0 || _transacao.Valor <= 0)
                {
                    Toast.MakeText(this, "Informe valores validos.", ToastLength.Short).Show();
                    return;
                }

                await _db.SalvarDespesaAsync(_despesa);
                await _db.SalvarTransacaoAsync(_transacao);
                await _db.AtualizaStatusAsync(_despesa.Id);

                Toast.MakeText(this, "Transacao atualizada!", ToastLength.Short).Show();
                Finish();
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, "Erro ao salvar: " + ex.Message, ToastLength.Long).Show();
            }
        }

        private async Task ConfirmarExclusaoAsync()
        {
            if (_transacao.Id == 0) return;

            new AlertDialog.Builder(this)
                .SetTitle("Excluir Transacao")
                .SetMessage("Deseja realmente excluir esta transacao?")
                .SetPositiveButton("Sim", async (senderAlert, args) =>
                {
                    try
                    {
                        int despesaId = _transacao.DespesaId;
                        await _db.DeletarTransacaoAsync(_transacao);
                        await _db.AtualizaStatusAsync(despesaId);

                        Toast.MakeText(this, "Transacao excluida!", ToastLength.Short).Show();
                        Finish();
                    }
                    catch (Exception ex)
                    {
                        Toast.MakeText(this, "Erro ao excluir: " + ex.Message, ToastLength.Long).Show();
                    }
                })
                .SetNegativeButton("Cancelar", (senderAlert, args) => { })
                .Show();
        }

        private static decimal ParseDecimal(string text)
        {
            return decimal.TryParse(text, NumberStyles.Any, new CultureInfo("pt-BR"), out decimal value)
                ? value
                : 0;
        }
    }
}
