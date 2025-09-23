using Android.Content;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.Globalization;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Nova Transação")]
    public class TransacaoCreateActivity : Activity
    {
        private EditText _edtValor, _edtObservacao, _edtData;
        private Button _btnSalvar;
        private int _despesaId;
        private DateTime _dataSelecionada;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_transacao_create);

            // Vincula campos da tela
            _edtValor = FindViewById<EditText>(Resource.Id.edtValor);
            _edtObservacao = FindViewById<EditText>(Resource.Id.edtObservacao);
            _edtData = FindViewById<EditText>(Resource.Id.edtData);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvar);

            _dataSelecionada = DateTime.Now;
            _edtData.Text = _dataSelecionada.ToString("dd/MM/yyyy");

            var db = new DatabaseService();

            // Preenche campos caso tenha vindo de uma despesa
            if (Intent.HasExtra("DespesaId"))
            {
                _despesaId = Intent.GetIntExtra("DespesaId", 0);
                PreencherCamposAsync(db, _despesaId);
            }

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

            // Corrigir vírgula em tempo real no campo de valor
            _edtValor.TextChanged += (s, e) =>
            {
                if (_edtValor.Text.Contains("."))
                {
                    _edtValor.Text = _edtValor.Text.Replace(".", ",");
                    _edtValor.SetSelection(_edtValor.Text.Length);
                }
            };

            // Botão Salvar
            _btnSalvar.Click += async (s, e) =>
            {
                if (_despesaId == 0)
                {
                    Toast.MakeText(this, "Despesa inválida.", ToastLength.Short).Show();
                    return;
                }

                if (!decimal.TryParse(_edtValor.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal valor) || valor <= 0)
                {
                    Toast.MakeText(this, "Informe um valor válido.", ToastLength.Short).Show();
                    return;
                }

                string observacao = _edtObservacao.Text?.Trim() ?? $"Transação da despesa {_despesaId}";

                var transacao = new Transacao
                {
                    DespesaId = _despesaId,
                    Valor = valor,
                    Observacao = observacao,
                    Data = _dataSelecionada
                };

                _btnSalvar.Enabled = false;
                try
                {
                    await db.SalvarTransacaoAsync(transacao);
                    await db.AtualizaStatusAsync(_despesaId);

                    Toast.MakeText(this, "Transação salva!", ToastLength.Short).Show();
                    Finish(); // Fecha a activity e volta para a lista
                }
                catch (Exception ex)
                {
                    Toast.MakeText(this, $"Erro ao salvar: {ex.Message}", ToastLength.Long).Show();
                }
                finally
                {
                    _btnSalvar.Enabled = true;
                }
            };
        }

        private async void PreencherCamposAsync(DatabaseService db, int despesaId)
        {
            try
            {
                var transacoes = await db.ListaTransacoesAsync(despesaId);

                double valorDespesa = Intent.GetDoubleExtra("Valor", 0);
                double totalPago = transacoes.Sum(x => (double)x.Valor);
                double valorRestante = valorDespesa - totalPago;

                RunOnUiThread(() =>
                {
                    _edtValor.Text = valorRestante.ToString("F2", CultureInfo.InvariantCulture);
                    _edtObservacao.Text = Intent.GetStringExtra("Observacao") ?? $"{Intent.GetStringExtra("Descricao")}";
                    _dataSelecionada = new DateTime(Intent.GetLongExtra("Vencimento", DateTime.Now.Ticks));
                    _edtData.Text = _dataSelecionada.ToString("dd/MM/yyyy");
                });
            }
            catch (Exception ex)
            {
                RunOnUiThread(() =>
                {
                    Toast.MakeText(this, $"Erro ao carregar despesa: {ex.Message}", ToastLength.Long).Show();
                });
            }
        }
    }
}
