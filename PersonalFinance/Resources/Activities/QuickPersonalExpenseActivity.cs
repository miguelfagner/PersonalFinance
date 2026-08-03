using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using PersonalFinance.Resources.Helpers;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using PersonalFinance.Resources.Widgets;
using System.Globalization;

namespace PersonalFinance.Resources.Activities;

[Activity(
    Label = "Lançar gasto pessoal",
    Exported = false,
    Theme = "@style/QuickPersonalExpenseTheme")]
public sealed class QuickPersonalExpenseActivity : Activity
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private Spinner _spinnerDespesa = null!;
    private TextView _txtDestino = null!;
    private TextView _txtSemOrcamento = null!;
    private EditText _edtDescricao = null!;
    private EditText _edtValor = null!;
    private EditText _edtData = null!;
    private Button _btnSalvar = null!;
    private Button _btnCancelar = null!;

    private readonly DatabaseService _db = new();
    private List<Despesa> _despesas = [];
    private DateTime _dataSelecionada = DateTime.Today;
    private bool _salvando;
    private int _versaoCarregamento;

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_gasto_pessoal_rapido);

        _spinnerDespesa = FindViewById<Spinner>(Resource.Id.spinnerDespesaPessoal)!;
        _txtDestino = FindViewById<TextView>(Resource.Id.txtDestinoPessoal)!;
        _txtSemOrcamento = FindViewById<TextView>(Resource.Id.txtSemOrcamentoPessoal)!;
        _edtDescricao = FindViewById<EditText>(Resource.Id.edtDescricaoGastoPessoal)!;
        _edtValor = FindViewById<EditText>(Resource.Id.edtValorGastoPessoal)!;
        _edtData = FindViewById<EditText>(Resource.Id.edtDataGastoPessoal)!;
        _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvarGastoPessoal)!;
        _btnCancelar = FindViewById<Button>(Resource.Id.btnCancelarGastoPessoal)!;

        _dataSelecionada = savedInstanceState?.GetLong("DataSelecionada") is long ticks && ticks > 0
            ? new DateTime(ticks)
            : DateTime.Today;
        _edtData.Text = _dataSelecionada.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        MoneyInputFormatter.Configure(_edtValor);
        ConfigurarEventos();
        await CarregarDespesasPessoaisAsync();
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        outState.PutLong("DataSelecionada", _dataSelecionada.Ticks);
        base.OnSaveInstanceState(outState);
    }

    private void ConfigurarEventos()
    {
        _edtData.Click += (s, e) =>
        {
            var dialog = new DatePickerDialog(
                this,
                async (sender, args) =>
                {
                    bool mudouCompetencia = args.Date.Month != _dataSelecionada.Month
                        || args.Date.Year != _dataSelecionada.Year;

                    _dataSelecionada = args.Date;
                    _edtData.Text = _dataSelecionada.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

                    if (mudouCompetencia)
                        await CarregarDespesasPessoaisAsync();
                },
                _dataSelecionada.Year,
                _dataSelecionada.Month - 1,
                _dataSelecionada.Day);

            dialog.Show();
        };

        _spinnerDespesa.ItemSelected += (s, e) => AtualizarDestinoSelecionado();
        _btnCancelar.Click += (s, e) => Finish();
        _btnSalvar.Click += async (s, e) => await SalvarAsync();
    }

    private async Task CarregarDespesasPessoaisAsync()
    {
        int versao = ++_versaoCarregamento;
        int despesaSelecionada = ObterDespesaSelecionada()?.Id ?? 0;
        DefinirCarregando(true);

        try
        {
            var despesas = await _db.ListaDespesasPessoaisAsync(_dataSelecionada);

            if (versao != _versaoCarregamento)
                return;

            _despesas = despesas;
            var nomes = _despesas.Count == 0
                ? new[] { "Nenhum orçamento PESSOAL neste mês" }
                : _despesas.Select(FormatarDespesa).ToArray();

            _spinnerDespesa.Adapter = FormOptions.CreateSpinnerAdapter(this, nomes);

            int indiceAnterior = _despesas.FindIndex(x => x.Id == despesaSelecionada);
            _spinnerDespesa.SetSelection(indiceAnterior >= 0 ? indiceAnterior : 0);
            AplicarEstadoDeDisponibilidade();
        }
        catch (Exception ex)
        {
            if (versao != _versaoCarregamento)
                return;

            _despesas = [];
            _spinnerDespesa.Adapter = FormOptions.CreateSpinnerAdapter(
                this,
                ["Falha ao carregar orçamentos"]);
            AplicarEstadoDeDisponibilidade();
            Toast.MakeText(this, $"Erro ao carregar: {ex.Message}", ToastLength.Long)?.Show();
        }
        finally
        {
            if (versao == _versaoCarregamento)
                DefinirCarregando(false);
        }
    }

    private void AplicarEstadoDeDisponibilidade()
    {
        bool possuiDespesa = _despesas.Count > 0;
        _spinnerDespesa.Enabled = _despesas.Count > 1;
        _txtSemOrcamento.Visibility = possuiDespesa ? ViewStates.Gone : ViewStates.Visible;
        _btnSalvar.Enabled = possuiDespesa && !_salvando;
        AtualizarDestinoSelecionado();
    }

    private void AtualizarDestinoSelecionado()
    {
        var despesa = ObterDespesaSelecionada();

        _txtDestino.Text = despesa is null
            ? $"Competência: {_dataSelecionada.ToString("MM/yyyy", CultureInfo.InvariantCulture)}"
            : $"Destino: {FormatarDespesa(despesa)}";
    }

    private async Task SalvarAsync()
    {
        if (_salvando)
            return;

        var despesa = ObterDespesaSelecionada();
        if (despesa is null)
        {
            Toast.MakeText(
                this,
                "Não existe uma despesa PESSOAL para a competência escolhida.",
                ToastLength.Long)?.Show();
            return;
        }

        string descricao = _edtDescricao.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(descricao))
        {
            _edtDescricao.Error = "Informe uma descrição.";
            _edtDescricao.RequestFocus();
            return;
        }

        decimal valor = MoneyInputFormatter.Parse(_edtValor.Text);
        if (valor <= 0m)
        {
            _edtValor.Error = "Informe um valor maior que zero.";
            _edtValor.RequestFocus();
            return;
        }

        var transacao = new Transacao
        {
            DespesaId = despesa.Id,
            Observacao = descricao,
            Valor = valor,
            Data = _dataSelecionada
        };

        _salvando = true;
        _btnSalvar.Enabled = false;

        try
        {
            await _db.SalvarGastoPessoalAsync(transacao);
            await PersonalFinanceWidgetProvider.AtualizarTodosAsync(this);

            Toast.MakeText(this, "Gasto pessoal lançado.", ToastLength.Short)?.Show();
            SetResult(Result.Ok);
            Finish();
        }
        catch (Exception ex)
        {
            Toast.MakeText(this, $"Erro ao salvar: {ex.Message}", ToastLength.Long)?.Show();
        }
        finally
        {
            _salvando = false;

            if (!IsFinishing)
                _btnSalvar.Enabled = _despesas.Count > 0;
        }
    }

    private Despesa? ObterDespesaSelecionada()
    {
        int indice = _spinnerDespesa?.SelectedItemPosition ?? -1;
        return indice >= 0 && indice < _despesas.Count ? _despesas[indice] : null;
    }

    private void DefinirCarregando(bool carregando)
    {
        _spinnerDespesa.Enabled = !carregando && _despesas.Count > 1;
        _btnSalvar.Enabled = !carregando && !_salvando && _despesas.Count > 0;
        _btnSalvar.Text = carregando ? "Carregando..." : "Lançar gasto";
    }

    private static string FormatarDespesa(Despesa despesa)
    {
        string descricao = string.IsNullOrWhiteSpace(despesa.Descricao)
            ? "Gasto pessoal"
            : despesa.Descricao;
        return $"{descricao} - {despesa.Valor.ToString("C", PtBr)}";
    }
}
