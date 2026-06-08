using Android.Content;
using Android.Views;
using PersonalFinance.Resources.Helpers;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.Globalization;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Editar Despesa")]
    public class DespesaEditActivity : Activity
    {
        private Spinner _spinnerReceita, _spinnerCategoria;
        private EditText _edtVencimento, _edtNParcela, _edtDescricao, _edtValor;
        private Button _btnSalvar, _btnExcluir, _btnQuitar;

        private DatabaseService _db;
        private Despesa _despesa;
        private List<Receita> _receitas;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (ActionBar != null)
                ActionBar.Hide();

            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.LightStatusBar;
            SetContentView(Resource.Layout.activity_despesa_edit);

            _db = new DatabaseService();

            int despesaId = Intent.GetIntExtra("DespesaId", 0);
            if (despesaId == 0)
            {
                Toast.MakeText(this, "Erro ao carregar despesa.", ToastLength.Short).Show();
                Finish();
                return;
            }

            _despesa = await _db.PegarDespesaAsync(despesaId);
            if (_despesa == null)
            {
                Toast.MakeText(this, "Despesa não encontrada.", ToastLength.Short).Show();
                Finish();
                return;
            }

            _spinnerReceita = FindViewById<Spinner>(Resource.Id.spinnerReceita);
            _spinnerCategoria = FindViewById<Spinner>(Resource.Id.spinnerCategoria);
            _edtVencimento = FindViewById<EditText>(Resource.Id.edtVencimento);
            _edtNParcela = FindViewById<EditText>(Resource.Id.edtNParcela);
            _edtDescricao = FindViewById<EditText>(Resource.Id.edtDescricao);
            _edtValor = FindViewById<EditText>(Resource.Id.edtValor);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvar);
            _btnExcluir = FindViewById<Button>(Resource.Id.btnExcluirDespesa);
            _btnQuitar = FindViewById<Button>(Resource.Id.btnQuitarDespesa);

            _receitas = await _db.ListaReceitasAsync();
            _spinnerReceita.Adapter = FormOptions.CreateSpinnerAdapter(this, _receitas.Select(r => r.FontePagadora));

            int receitaIndex = _receitas.FindIndex(r => r.Id == _despesa.ReceitaId);
            if (receitaIndex >= 0) _spinnerReceita.SetSelection(receitaIndex);

            var categorias = FormOptions.WithCurrentOption(FormOptions.CategoriasDespesa, _despesa.Categoria);
            _spinnerCategoria.Adapter = FormOptions.CreateSpinnerAdapter(this, categorias);

            int categoriaIndex = categorias.FindIndex(c => string.Equals(c, _despesa.Categoria, StringComparison.OrdinalIgnoreCase));
            if (categoriaIndex >= 0) _spinnerCategoria.SetSelection(categoriaIndex);

            _edtVencimento.Text = _despesa.Vencimento.ToString("dd/MM/yyyy");
            _edtNParcela.Text = _despesa.NParcela.ToString();
            _edtDescricao.Text = _despesa.Descricao;
            _edtValor.Text = _despesa.Valor.ToString("F2", new CultureInfo("pt-BR"));

            _edtVencimento.Click += (s, e) =>
            {
                DateTime dataAtual = _despesa.Vencimento != default ? _despesa.Vencimento : DateTime.Now;

                var dialog = new DatePickerDialog(this, (sender, args) =>
                {
                    _edtVencimento.Text = args.Date.ToString("dd/MM/yyyy");
                }, dataAtual.Year, dataAtual.Month - 1, dataAtual.Day);

                dialog.Show();
            };

            _btnSalvar.Click += async (s, e) =>
            {
                try
                {
                    var selectedIndex = _spinnerReceita.SelectedItemPosition;
                    if (selectedIndex < 0 || selectedIndex >= _receitas.Count)
                    {
                        Toast.MakeText(this, "Selecione uma receita.", ToastLength.Short).Show();
                        return;
                    }

                    _despesa.ReceitaId = _receitas[selectedIndex].Id;
                    _despesa.Descricao = _edtDescricao.Text?.Trim().ToUpperInvariant();
                    _despesa.Categoria = _spinnerCategoria.SelectedItem?.ToString();
                    _despesa.NParcela = int.TryParse(_edtNParcela.Text, out int nParcela) ? nParcela : 0;

                    if (DateTime.TryParseExact(_edtVencimento.Text, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime venc))
                        _despesa.Vencimento = venc;

                    _despesa.Valor = decimal.TryParse(_edtValor.Text, NumberStyles.Any, new CultureInfo("pt-BR"), out decimal valor) ? valor : 0;

                    await _db.SalvarDespesaAsync(_despesa);
                    await _db.AtualizaStatusAsync(_despesa.Id);

                    Toast.MakeText(this, "Despesa atualizada!", ToastLength.Short).Show();
                    Finish();
                }
                catch (Exception ex)
                {
                    Toast.MakeText(this, "Erro ao salvar: " + ex.Message, ToastLength.Long).Show();
                }
            };

            _btnExcluir.Click += async (s, e) =>
            {
                new AlertDialog.Builder(this)
                    .SetTitle("Excluir Despesa")
                    .SetMessage("Tem certeza que deseja excluir esta despesa?")
                    .SetPositiveButton("Sim", async (senderAlert, args) =>
                    {
                        try
                        {
                            await _db.DeletarDespesaAsync(_despesa);

                            Toast.MakeText(this, "Despesa excluída!", ToastLength.Short).Show();
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

            _btnQuitar.Click += async (s, e) =>
            {
                await QuitarDespesaAsync(_despesa);
            };
        }

        private async Task QuitarDespesaAsync(Despesa despesa)
        {
            try
            {
                var transacoes = await _db.ListaTransacoesAsync(despesa.Id);
                var pago = transacoes.Sum(x => x.Valor);

                if (pago >= despesa.Valor)
                {
                    Toast.MakeText(this, "Despesa já está quitada!", ToastLength.Short).Show();
                    return;
                }

                var valorRestante = despesa.Valor - pago;

                var transacao = new Transacao
                {
                    DespesaId = despesa.Id,
                    Valor = valorRestante,
                    Data = DateTime.Now,
                    Observacao = "DESPESA DE " + despesa.Categoria
                };

                await _db.SalvarTransacaoAsync(transacao);
                await _db.AtualizaStatusAsync(despesa.Id);

                despesa.Sttatus = true;

                Toast.MakeText(this, $"Despesa '{despesa.Descricao}' quitada!", ToastLength.Short).Show();
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Erro ao quitar: {ex.Message}", ToastLength.Long).Show();
            }
        }
    }
}
