using Android.Content;
using Android.Views;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.Globalization;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Editar Despesa")]
    public class DespesaEditActivity : Activity
    {
        private Spinner _spinnerReceita;
        private EditText _edtVencimento, _edtNParcela, _edtDescricao, _edtCategoria, _edtValor;
        private Button _btnSalvar, _btnExcluir, _btnQuitar;

        private DatabaseService _db;
        private Despesa _despesa;
        private List<Receita> _receitas;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (ActionBar != null)
                ActionBar.Hide();

            // Cor da StatusBar (ícones escuros)
            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.LightStatusBar;

            SetContentView(Resource.Layout.activity_despesa_edit);

            // Inicializar DB
            _db = new DatabaseService();

            // Obter ID da despesa que veio da lista
            int despesaId = Intent.GetIntExtra("DespesaId", 0);
            if (despesaId == 0)
            {
                Toast.MakeText(this, "Erro ao carregar despesa.", ToastLength.Short).Show();
                Finish();
                return;
            }

            // Carregar despesa
            _despesa = await _db.PegarDespesaAsync(despesaId);
            if (_despesa == null)
            {
                Toast.MakeText(this, "Despesa não encontrada.", ToastLength.Short).Show();
                Finish();
                return;
            }

            // Vincular componentes
            _spinnerReceita = FindViewById<Spinner>(Resource.Id.spinnerReceita);
            _edtVencimento = FindViewById<EditText>(Resource.Id.edtVencimento);
            _edtNParcela = FindViewById<EditText>(Resource.Id.edtNParcela);
            _edtDescricao = FindViewById<EditText>(Resource.Id.edtDescricao);
            _edtCategoria = FindViewById<EditText>(Resource.Id.edtCategoria);
            _edtValor = FindViewById<EditText>(Resource.Id.edtValor);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvar);
            _btnExcluir = FindViewById<Button>(Resource.Id.btnExcluirDespesa);
            _btnQuitar = FindViewById<Button>(Resource.Id.btnQuitarDespesa);

            // Carregar receitas no spinner
            _receitas = await _db.ListaReceitasAsync();
            var receitasNomes = _receitas.Select(r => r.FontePagadora).ToList();
            var adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, receitasNomes);
            adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            _spinnerReceita.Adapter = adapter;

            // Selecionar receita da despesa
            int index = _receitas.FindIndex(r => r.Id == _despesa.ReceitaId);
            if (index >= 0) _spinnerReceita.SetSelection(index);

            // Preencher campos
            _edtVencimento.Text = _despesa.Vencimento.ToString("dd/MM/yyyy");
            _edtNParcela.Text = _despesa.NParcela.ToString();
            _edtDescricao.Text = _despesa.Descricao;
            _edtCategoria.Text = _despesa.Categoria;
            _edtValor.Text = _despesa.Valor.ToString("F2", new CultureInfo("pt-BR"));

            // Abrir DatePicker ao clicar no vencimento
            _edtVencimento.Click += (s, e) =>
            {
                DateTime hoje = _despesa.Vencimento != default ? _despesa.Vencimento : DateTime.Now;

                DatePickerDialog dialog = new DatePickerDialog(this, (sender, args) =>
                {
                    _edtVencimento.Text = args.Date.ToString("dd/MM/yyyy");
                }, hoje.Year, hoje.Month - 1, hoje.Day);

                dialog.Show();
            };

            // Clique no botão Salvar
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
                    _despesa.Descricao = _edtDescricao.Text;
                    _despesa.Categoria = _edtCategoria.Text;
                    _despesa.NParcela = int.TryParse(_edtNParcela.Text, out int nParcela) ? nParcela : 0;

                    if (DateTime.TryParseExact(_edtVencimento.Text, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime venc))
                        _despesa.Vencimento = venc;

                    _despesa.Valor = decimal.TryParse(_edtValor.Text, NumberStyles.Any, new CultureInfo("pt-BR"), out decimal valor) ? valor : 0;

                    await _db.SalvarDespesaAsync(_despesa);

                    // Atualiza o status da despesa com base nas transações associadas
                    await _db.AtualizaStatusAsync(_despesa.Id);

                    Toast.MakeText(this, "Despesa atualizada!", ToastLength.Short).Show();
                    Finish();
                }
                catch (Exception ex)
                {
                    Toast.MakeText(this, "Erro ao salvar: " + ex.Message, ToastLength.Long).Show();
                }
            };

            // Clique no botão Excluir
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

            // Clique no botão Quitar
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

                // Cria a transação no banco
                var transacao = new Transacao
                {
                    DespesaId = despesa.Id,
                    Valor = valorRestante,
                    Data = DateTime.Now,
                    Observacao = "DESPESA DE " + despesa.Categoria
                };

                await _db.SalvarTransacaoAsync(transacao);

                // Atualiza o status da despesa no banco
                await _db.AtualizaStatusAsync(despesa.Id);

                // Atualiza o status local
                despesa.Sttatus = true;

                Toast.MakeText(this, $"Despesa '{despesa.Descricao}' quitada!", ToastLength.Short).Show();
            }
            catch (System.Exception ex)
            {
                Toast.MakeText(this, $"Erro ao quitar: {ex.Message}", ToastLength.Long).Show();
            }
        }
    }
}
