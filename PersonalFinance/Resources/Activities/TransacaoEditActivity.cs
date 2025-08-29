using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Editar Transação")]
    public class TransacaoEditActivity : Activity
    {
        private EditText _edtData, _edtValor, _edtObservacao;
        private Button _btnSalvar, _btnExcluir;
        private TextView _txtDespesa, _txtReceita;
        private DatabaseService _db;
        private Transacao _transacao;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (ActionBar != null)
                ActionBar.Hide();

            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.LightStatusBar;

            SetContentView(Resource.Layout.activity_transacao_edit);

            _db = new DatabaseService();

            // Obter ID da transação
            int transacaoId = Intent.GetIntExtra("TransacaoId", 0);

            if (transacaoId > 0)
                _transacao = await _db.PegarTransacaoAsync(transacaoId);

            if (_transacao == null)
                _transacao = new Transacao { Data = DateTime.Today };

            // Vincular componentes
            _edtData = FindViewById<EditText>(Resource.Id.edtData);
            _edtValor = FindViewById<EditText>(Resource.Id.edtValor);
            _edtObservacao = FindViewById<EditText>(Resource.Id.edtObservacao);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvarTransacao);
            _btnExcluir = FindViewById<Button>(Resource.Id.btnExcluirTransacao);
            _txtDespesa = FindViewById<TextView>(Resource.Id.txtDespesa);
            _txtReceita = FindViewById<TextView>(Resource.Id.txtReceita);

            // Preencher campos
            _edtData.Text = _transacao.Data.ToString("dd/MM/yyyy");
            _edtValor.Text = _transacao.Valor.ToString("F2", new CultureInfo("pt-BR"));
            _edtObservacao.Text = _transacao.Observacao;

            // Carregar despesa e receita relacionadas
            await CarregarDespesaEReceita();

            // DatePicker
            _edtData.Click += (s, e) =>
            {
                DateTime hoje = _transacao.Data != default ? _transacao.Data : DateTime.Today;

                var dialog = new DatePickerDialog(this, (sender, args) =>
                {
                    _edtData.Text = args.Date.ToString("dd/MM/yyyy");
                }, hoje.Year, hoje.Month - 1, hoje.Day);

                dialog.Show();
            };

            // Corrigir separador decimal
            _edtValor.TextChanged += (s, e) =>
            {
                if (_edtValor.Text.Contains("."))
                {
                    _edtValor.Text = _edtValor.Text.Replace(".", ",");
                    _edtValor.SetSelection(_edtValor.Text.Length);
                }
            };

            // Salvar transação
            _btnSalvar.Click += async (s, e) => await SalvarTransacaoAsync();

            // Excluir transação
            _btnExcluir.Click += async (s, e) =>
            {
                if (_transacao.Id == 0) return; // não existe no banco

                new AlertDialog.Builder(this)
                    .SetTitle("Excluir Transação")
                    .SetMessage("Deseja realmente excluir esta transação?")
                    .SetPositiveButton("Sim", async (senderAlert, args) =>
                    {
                        try
                        {
                            await _db.DeletarTransacaoAsync(_transacao);
                            await _db.AtualizaStatusAsync(_transacao.DespesaId);
                            
                            Toast.MakeText(this, "Transação excluída!", ToastLength.Short).Show();
                            Finish();
                        }
                        catch (Exception ex)
                        {
                            Toast.MakeText(this, "Erro ao excluir: " + ex.Message, ToastLength.Long).Show();
                        }
                    })
                    .SetNegativeButton("Cancelar", (senderAlert, args) => { })
                    .Show();
            };
        }

        private async Task CarregarDespesaEReceita()
        {
            if (_transacao?.DespesaId > 0)
            {
                _transacao.Despesa = await _db.PegarDespesaAsync(_transacao.DespesaId);

                if (_transacao.Despesa != null)
                {
                    _txtDespesa.Text = $"Despesa: {_transacao.Despesa.Descricao} (R$ {_transacao.Despesa.Valor:F2})";

                    if (_transacao.Despesa.ReceitaId > 0)
                    {
                        _transacao.Despesa.Receita = await _db.PegarReceitaAsync(_transacao.Despesa.ReceitaId);

                        if (_transacao.Despesa.Receita != null)
                        {
                            _txtReceita.Text = $"Receita: {_transacao.Despesa.Receita.FontePagadora} (R$ {_transacao.Despesa.Receita.Valor:F2})";
                        }
                        else
                        {
                            _txtReceita.Text = "Receita: não vinculada";
                        }
                    }
                }
                else
                {
                    _txtDespesa.Text = "Despesa: não vinculada";
                    _txtReceita.Text = "Receita: -";
                }
            }
            else
            {
                _txtDespesa.Text = "Despesa: não vinculada";
                _txtReceita.Text = "Receita: -";
            }
        }

        private async Task SalvarTransacaoAsync()
        {
            try
            {
                if (DateTime.TryParseExact(_edtData.Text, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime data))
                    _transacao.Data = data;

                _transacao.Valor = decimal.TryParse(_edtValor.Text, NumberStyles.Any, new CultureInfo("pt-BR"), out decimal valor) ? valor : 0;
                _transacao.Observacao = _edtObservacao.Text;

                await _db.SalvarTransacaoAsync(_transacao);
                await _db.AtualizaStatusAsync(_transacao.DespesaId);

                Toast.MakeText(this, "Transação salva!", ToastLength.Short).Show();
                Finish();
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, "Erro ao salvar: " + ex.Message, ToastLength.Long).Show();
            }
        }
    }
}
