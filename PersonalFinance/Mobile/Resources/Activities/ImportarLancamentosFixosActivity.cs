using Android.Graphics;
using Android.Views;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.Globalization;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Importar lançamentos fixos")]
    public class ImportarLancamentosFixosActivity : FinanceActivity
    {
        private readonly CultureInfo _ptBr = new("pt-BR");
        private readonly Dictionary<int, CheckBox> _receitaChecks = new();
        private readonly Dictionary<int, CheckBox> _despesaChecks = new();
        private FinanceRepository _db = null!;
        private EditText _edtCompetencia = null!;
        private LinearLayout _containerReceitas = null!;
        private LinearLayout _containerDespesas = null!;
        private TextView _tvResumo = null!;
        private Button _btnImportar = null!;
        private DateTime _competencia = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private List<Receita> _receitas = new();
        private List<Despesa> _despesas = new();

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_importar_lancamentos_fixos);

            _db = new FinanceRepository();
            _edtCompetencia = FindViewById<EditText>(Resource.Id.edtCompetenciaImportacao)!;
            _containerReceitas = FindViewById<LinearLayout>(Resource.Id.containerReceitasImportacao)!;
            _containerDespesas = FindViewById<LinearLayout>(Resource.Id.containerDespesasImportacao)!;
            _tvResumo = FindViewById<TextView>(Resource.Id.tvResumoImportacao)!;
            _btnImportar = FindViewById<Button>(Resource.Id.btnImportarSelecionados)!;

            AtualizarCompetencia();

            _edtCompetencia.Click += (s, e) => AbrirSeletorMes();
            FindViewById<Button>(Resource.Id.btnCarregarImportacao)!.Click += async (s, e) => await CarregarAsync();
            _btnImportar.Click += async (s, e) => await ImportarAsync();
        }

        private void AbrirSeletorMes()
        {
            var dialog = new DatePickerDialog(this, (s, e) =>
            {
                _competencia = new DateTime(e.Date.Year, e.Date.Month, 1);
                AtualizarCompetencia();
                _containerReceitas.RemoveAllViews();
                _containerDespesas.RemoveAllViews();
                _btnImportar.Enabled = false;
                _tvResumo.Text = "Toque em carregar lançamentos para revisar o mês.";
            }, _competencia.Year, _competencia.Month - 1, 1);
            dialog.Show();
        }

        private void AtualizarCompetencia() =>
            _edtCompetencia.Text = _competencia.ToString("MM/yyyy", _ptBr);

        private async Task CarregarAsync()
        {
            try
            {
                var fim = _competencia.AddMonths(1);
                var receitasTask = _db.ListaReceitasAsync(_competencia, fim);
                var despesasTask = _db.ListaDespesasAsync(_competencia, fim);
                await Task.WhenAll(receitasTask, despesasTask);
                _receitas = receitasTask.Result;
                _despesas = despesasTask.Result;
                MontarListas();
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Erro ao carregar: {ex.Message}", ToastLength.Long)?.Show();
            }
        }

        private void MontarListas()
        {
            _containerReceitas.RemoveAllViews();
            _containerDespesas.RemoveAllViews();
            _receitaChecks.Clear();
            _despesaChecks.Clear();

            foreach (var receita in _receitas)
            {
                bool importada = receita.ReceitaFixaId.HasValue;
                var check = CriarCheckBox(
                    $"{receita.FontePagadora} · R$ {receita.Valor.ToString("N2", _ptBr)}" +
                    (importada ? "\nJÁ IMPORTADA" : string.Empty),
                    !importada);
                check.CheckedChange += (s, e) =>
                {
                    if (!e.IsChecked)
                    {
                        foreach (var despesa in _despesas.Where(d => d.ReceitaId == receita.Id))
                        {
                            if (_despesaChecks.TryGetValue(despesa.Id, out var dependente))
                                dependente.Checked = false;
                        }
                    }
                    AtualizarEstadoBotao();
                };
                _receitaChecks[receita.Id] = check;
                _containerReceitas.AddView(check);
            }

            foreach (var despesa in _despesas)
            {
                bool importada = despesa.DespesaFixaId.HasValue;
                var check = CriarCheckBox(
                    $"{despesa.Descricao} · {despesa.Categoria}\n" +
                    $"R$ {despesa.Valor.ToString("N2", _ptBr)} · vence dia {despesa.Vencimento.Day}" +
                    (importada ? "\nJÁ IMPORTADA" : string.Empty),
                    !importada);
                check.CheckedChange += (s, e) =>
                {
                    if (e.IsChecked && _receitaChecks.TryGetValue(despesa.ReceitaId, out var receitaCheck))
                        receitaCheck.Checked = true;
                    AtualizarEstadoBotao();
                };
                _despesaChecks[despesa.Id] = check;
                _containerDespesas.AddView(check);
            }

            _tvResumo.Text = _receitas.Count == 0 && _despesas.Count == 0
                ? "Nenhum lançamento encontrado nessa competência."
                : $"{_receitas.Count} receitas e {_despesas.Count} despesas encontradas.";
            AtualizarEstadoBotao();
        }

        private CheckBox CriarCheckBox(string texto, bool habilitado)
        {
            var check = new CheckBox(this)
            {
                Text = texto,
                Enabled = habilitado
            };
            check.SetTextColor(Color.ParseColor("#17212B"));
            check.SetTextSize(Android.Util.ComplexUnitType.Sp, 15);
            check.SetPadding(16, 14, 16, 14);
            check.SetBackgroundResource(Resource.Drawable.bg_card);
            var layout = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
            {
                BottomMargin = 10
            };
            check.LayoutParameters = layout;
            return check;
        }

        private void AtualizarEstadoBotao()
        {
            int receitas = _receitaChecks.Values.Count(x => x.Enabled && x.Checked);
            int despesas = _despesaChecks.Values.Count(x => x.Enabled && x.Checked);
            _btnImportar.Enabled = receitas + despesas > 0;
            if (_receitas.Count + _despesas.Count > 0)
                _btnImportar.Text = $"Criar {receitas + despesas} modelos fixos";
        }

        private async Task ImportarAsync()
        {
            var receitaIds = _receitaChecks.Where(x => x.Value.Enabled && x.Value.Checked).Select(x => x.Key).ToList();
            var despesaIds = _despesaChecks.Where(x => x.Value.Enabled && x.Value.Checked).Select(x => x.Key).ToList();

            _btnImportar.Enabled = false;
            try
            {
                var resultado = await _db.ImportarLancamentosComoFixosAsync(
                    _competencia,
                    receitaIds,
                    despesaIds);
                new AlertDialog.Builder(this)
                    .SetTitle("Importação concluída")
                    .SetMessage(
                        $"Receitas: {resultado.ReceitasCriadas} criadas e " +
                        $"{resultado.ReceitasReutilizadas} reutilizadas.\n" +
                        $"Despesas: {resultado.DespesasCriadas} criadas e " +
                        $"{resultado.DespesasReutilizadas} reutilizadas.\n" +
                        $"Ignorados: {resultado.Ignorados}.")
                    .SetPositiveButton("OK", (s, e) => { })
                    .Show();
                await CarregarAsync();
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Erro ao importar: {ex.Message}", ToastLength.Long)?.Show();
                AtualizarEstadoBotao();
            }
        }
    }
}
