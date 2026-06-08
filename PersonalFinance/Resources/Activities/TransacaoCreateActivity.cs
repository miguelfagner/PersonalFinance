using Android.Content;
using PersonalFinance.Resources.Helpers;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.Globalization;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Nova Transação")]
    public class TransacaoCreateActivity : Activity
    {
        private Spinner _spinnerDespesa;
        private EditText _edtValor, _edtObservacao, _edtData;
        private Button _btnSalvar;
        private List<Despesa> _despesas;
        private DateTime _dataSelecionada;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_transacao_create);

            _spinnerDespesa = FindViewById<Spinner>(Resource.Id.spinnerDespesa);
            _edtValor = FindViewById<EditText>(Resource.Id.edtValor);
            _edtObservacao = FindViewById<EditText>(Resource.Id.edtObservacao);
            _edtData = FindViewById<EditText>(Resource.Id.edtData);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvar);

            _dataSelecionada = DateTime.Now;
            _edtData.Text = _dataSelecionada.ToString("dd/MM/yyyy");

            var db = new DatabaseService();
            _despesas = await db.ListaDespesasAsync();
            _spinnerDespesa.Adapter = FormOptions.CreateSpinnerAdapter(this, _despesas.Select(FormatarDespesa));

            if (Intent.HasExtra("DespesaId"))
            {
                int despesaId = Intent.GetIntExtra("DespesaId", 0);
                int index = _despesas.FindIndex(d => d.Id == despesaId);

                if (index >= 0)
                {
                    _spinnerDespesa.SetSelection(index);
                    await PreencherCamposAsync(db, _despesas[index]);
                }
            }

            _spinnerDespesa.ItemSelected += async (s, e) =>
            {
                if (e.Position >= 0 && e.Position < _despesas.Count && string.IsNullOrWhiteSpace(_edtValor.Text))
                {
                    await PreencherCamposAsync(db, _despesas[e.Position]);
                }
            };

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

            _edtValor.TextChanged += (s, e) =>
            {
                if (_edtValor.Text.Contains("."))
                {
                    _edtValor.Text = _edtValor.Text.Replace(".", ",");
                    _edtValor.SetSelection(_edtValor.Text.Length);
                }
            };

            _btnSalvar.Click += async (s, e) =>
            {
                var selectedIndex = _spinnerDespesa.SelectedItemPosition;
                if (selectedIndex < 0 || selectedIndex >= _despesas.Count)
                {
                    Toast.MakeText(this, "Selecione uma despesa.", ToastLength.Short).Show();
                    return;
                }

                if (!decimal.TryParse(_edtValor.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal valor) || valor <= 0)
                {
                    Toast.MakeText(this, "Informe um valor válido.", ToastLength.Short).Show();
                    return;
                }

                var despesa = _despesas[selectedIndex];
                string observacao = _edtObservacao.Text?.Trim() ?? $"Transação da despesa {despesa.Id}";

                var transacao = new Transacao
                {
                    DespesaId = despesa.Id,
                    Valor = valor,
                    Observacao = observacao,
                    Data = _dataSelecionada
                };

                _btnSalvar.Enabled = false;
                try
                {
                    await db.SalvarTransacaoAsync(transacao);
                    await db.AtualizaStatusAsync(despesa.Id);

                    Toast.MakeText(this, "Transação salva!", ToastLength.Short).Show();
                    Finish();
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

        private async Task PreencherCamposAsync(DatabaseService db, Despesa despesa)
        {
            try
            {
                var transacoes = await db.ListaTransacoesAsync(despesa.Id);
                var valorRestante = despesa.Valor - transacoes.Sum(x => x.Valor);

                _edtValor.Text = valorRestante.ToString("F2", new CultureInfo("pt-BR"));
                _edtObservacao.Text = string.IsNullOrWhiteSpace(_edtObservacao.Text)
                    ? despesa.Descricao
                    : _edtObservacao.Text;
                _dataSelecionada = despesa.Vencimento == default ? DateTime.Today : despesa.Vencimento;
                _edtData.Text = _dataSelecionada.ToString("dd/MM/yyyy");
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Erro ao carregar despesa: {ex.Message}", ToastLength.Long).Show();
            }
        }

        private static string FormatarDespesa(Despesa despesa)
        {
            return $"{despesa.Descricao} - R$ {despesa.Valor:F2}";
        }
    }
}
