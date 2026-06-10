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
        private Spinner _spinnerDespesa;
        private EditText _edtDescricao, _edtValor, _edtData;
        private Button _btnSalvar, _btnExcluir;

        private DatabaseService _db;
        private Transacao _transacao;
        private List<Despesa> _despesas;
        private DateTime _dataSelecionada;

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
            if (_transacao == null)
            {
                Toast.MakeText(this, "Transacao nao encontrada.", ToastLength.Short).Show();
                Finish();
                return;
            }

            _spinnerDespesa = FindViewById<Spinner>(Resource.Id.spinnerDespesa);
            _edtDescricao = FindViewById<EditText>(Resource.Id.edtDescricao);
            _edtValor = FindViewById<EditText>(Resource.Id.edtValor);
            _edtData = FindViewById<EditText>(Resource.Id.edtData);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvarTransacao);
            _btnExcluir = FindViewById<Button>(Resource.Id.btnExcluirTransacao);

            if (!await CarregarDespesasAsync())
            {
                Toast.MakeText(this, "Despesa atual da transacao nao encontrada.", ToastLength.Short).Show();
                Finish();
                return;
            }

            PreencherCampos();
            ConfigurarSeletorDeData();
            ConfigurarCampoDeValor();

            _btnSalvar.Click += async (s, e) => await SalvarAsync();
            _btnExcluir.Click += async (s, e) => await ConfirmarExclusaoAsync();
        }

        private async Task<bool> CarregarDespesasAsync()
        {
            _despesas = await _db.ListaDespesasAsync();
            _spinnerDespesa.Adapter = FormOptions.CreateSpinnerAdapter(this, _despesas.Select(FormatarDespesa));

            int despesaIndex = _despesas.FindIndex(d => d.Id == _transacao.DespesaId);
            if (despesaIndex < 0)
                return false;

            _spinnerDespesa.SetSelection(despesaIndex);
            return true;
        }

        private void PreencherCampos()
        {
            _dataSelecionada = _transacao.Data == default ? DateTime.Today : _transacao.Data;

            _edtDescricao.Text = _transacao.Observacao;
            _edtValor.Text = _transacao.Valor.ToString("F2", new CultureInfo("pt-BR"));
            _edtData.Text = _dataSelecionada.ToString("dd/MM/yyyy");
        }

        private void ConfigurarSeletorDeData()
        {
            _edtData.Click += (s, e) =>
            {
                var dialog = new DatePickerDialog(this, (sender, args) =>
                {
                    _dataSelecionada = args.Date;
                    _edtData.Text = _dataSelecionada.ToString("dd/MM/yyyy");
                }, _dataSelecionada.Year, _dataSelecionada.Month - 1, _dataSelecionada.Day);

                dialog.Show();
            };
        }

        private void ConfigurarCampoDeValor()
        {
            MoneyInputFormatter.Configure(_edtValor);
        }

        private async Task SalvarAsync()
        {
            try
            {
                int despesaIndex = _spinnerDespesa.SelectedItemPosition;
                if (despesaIndex < 0 || despesaIndex >= _despesas.Count)
                {
                    Toast.MakeText(this, "Selecione uma despesa.", ToastLength.Short).Show();
                    return;
                }

                decimal valor = MoneyInputFormatter.Parse(_edtValor.Text);
                if (valor <= 0)
                {
                    Toast.MakeText(this, "Informe um valor valido.", ToastLength.Short).Show();
                    return;
                }

                int despesaAnteriorId = _transacao.DespesaId;
                int novaDespesaId = _despesas[despesaIndex].Id;

                _transacao.DespesaId = novaDespesaId;
                _transacao.Observacao = _edtDescricao.Text?.Trim();
                _transacao.Valor = valor;
                _transacao.Data = _dataSelecionada;

                _btnSalvar.Enabled = false;

                await _db.SalvarTransacaoAsync(_transacao);
                await _db.AtualizaStatusAsync(despesaAnteriorId);

                if (novaDespesaId != despesaAnteriorId)
                    await _db.AtualizaStatusAsync(novaDespesaId);

                Toast.MakeText(this, "Transacao atualizada!", ToastLength.Short).Show();
                Finish();
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, "Erro ao salvar: " + ex.Message, ToastLength.Long).Show();
            }
            finally
            {
                _btnSalvar.Enabled = true;
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

        private static string FormatarDespesa(Despesa despesa)
        {
            return $"{despesa.Descricao} - {despesa.Categoria} - R$ {despesa.Valor:F2}";
        }
    }
}
