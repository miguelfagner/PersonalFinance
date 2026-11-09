using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Views;
using PersonalFinance.Resources.Helpers;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using PersonalFinance.Resources.Widgets;
using System.Globalization;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Preparar mês")]
    public class PrepararMesActivity : FinanceActivity
    {
        private readonly CultureInfo _ptBr = new("pt-BR");
        private readonly Dictionary<int, CheckBox> _receitaChecks = new();
        private readonly Dictionary<int, CheckBox> _despesaChecks = new();
        private FinanceRepository _db = null!;
        private EditText _edtCompetencia = null!;
        private LinearLayout _containerReceitas = null!;
        private LinearLayout _containerDespesas = null!;
        private TextView _tvResumo = null!;
        private Button _btnGerar = null!;
        private DateTime _competencia = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private List<PreparacaoMensalItem> _itens = new();

        protected override async void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_preparar_mes);

            _db = new FinanceRepository();
            _edtCompetencia = FindViewById<EditText>(Resource.Id.edtCompetenciaPreparacao)!;
            _containerReceitas = FindViewById<LinearLayout>(Resource.Id.containerReceitasPreparacao)!;
            _containerDespesas = FindViewById<LinearLayout>(Resource.Id.containerDespesasPreparacao)!;
            _tvResumo = FindViewById<TextView>(Resource.Id.tvResumoPreparacao)!;
            _btnGerar = FindViewById<Button>(Resource.Id.btnGerarPreparacao)!;

            int mes = Intent.GetIntExtra("Mes", DateTime.Today.Month);
            int ano = Intent.GetIntExtra("Ano", DateTime.Today.Year);
            _competencia = new DateTime(ano, mes, 1);
            AtualizarCompetencia();

            _edtCompetencia.Click += (s, e) => AbrirSeletorMes();
            FindViewById<Button>(Resource.Id.btnCarregarPreparacao)!.Click += async (s, e) => await CarregarAsync();
            FindViewById<Button>(Resource.Id.btnCancelarPreparacao)!.Click += (s, e) => Finish();
            _btnGerar.Click += (s, e) => ConfirmarGeracao();

            await CarregarAsync();
        }

        private void AbrirSeletorMes()
        {
            var dialog = new DatePickerDialog(this, async (s, e) =>
            {
                _competencia = new DateTime(e.Date.Year, e.Date.Month, 1);
                AtualizarCompetencia();
                await CarregarAsync();
            }, _competencia.Year, _competencia.Month - 1, 1);
            dialog.Show();
        }

        private void AtualizarCompetencia()
        {
            _edtCompetencia.Text = _competencia.ToString("MM/yyyy", _ptBr);
            _btnGerar.Text = $"Gerar lançamentos de {_competencia:MM/yyyy}";
        }

        private async Task CarregarAsync()
        {
            _btnGerar.Enabled = false;
            try
            {
                _itens = await _db.PreverPreparacaoMensalAsync(_competencia);
                MontarItens();
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Erro ao carregar modelos: {ex.Message}", ToastLength.Long)?.Show();
            }
        }

        private void MontarItens()
        {
            _containerReceitas.RemoveAllViews();
            _containerDespesas.RemoveAllViews();
            _receitaChecks.Clear();
            _despesaChecks.Clear();

            foreach (var item in _itens.Where(x => EhReceita(x)))
            {
                var check = AdicionarItem(_containerReceitas, item);
                _receitaChecks[item.ModeloId] = check;
                check.CheckedChange += (s, e) =>
                {
                    item.Selecionado = e.IsChecked;
                    AtualizarTextoItem(item, check);
                    if (!e.IsChecked)
                    {
                        foreach (var dependente in _itens.Where(x => !EhReceita(x) && x.ReceitaFixaId == item.ModeloId))
                        {
                            dependente.Selecionado = false;
                            if (_despesaChecks.TryGetValue(dependente.ModeloId, out var despesaCheck))
                                despesaCheck.Checked = false;
                        }
                    }
                    AtualizarResumo();
                };
            }

            foreach (var item in _itens.Where(x => !EhReceita(x)))
            {
                var check = AdicionarItem(_containerDespesas, item);
                _despesaChecks[item.ModeloId] = check;
                check.CheckedChange += (s, e) =>
                {
                    item.Selecionado = e.IsChecked;
                    AtualizarTextoItem(item, check);
                    if (e.IsChecked && item.ReceitaFixaId.HasValue &&
                        _receitaChecks.TryGetValue(item.ReceitaFixaId.Value, out var receitaCheck) &&
                        receitaCheck.Enabled)
                    {
                        receitaCheck.Checked = true;
                    }
                    AtualizarResumo();
                };
            }

            if (_itens.Count == 0)
            {
                _tvResumo.Text = "Nenhum modelo fixo ativo. Cadastre ou reative modelos antes de preparar o mês.";
                _btnGerar.Enabled = false;
            }
            else
            {
                AtualizarResumo();
            }
        }

        private CheckBox AdicionarItem(LinearLayout destino, PreparacaoMensalItem item)
        {
            var card = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };
            card.SetPadding(14, 10, 14, 10);
            card.SetBackgroundResource(Resource.Drawable.bg_card);
            card.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
            {
                BottomMargin = 10
            };

            var check = new CheckBox(this)
            {
                Text = ObterRotulo(item) + ObterEstado(item),
                Checked = item.Selecionado && !item.JaLancado,
                Enabled = !item.JaLancado
            };
            check.SetTextColor(Color.ParseColor("#17212B"));
            check.SetTextSize(Android.Util.ComplexUnitType.Sp, 15);
            card.AddView(check);

            if (!string.IsNullOrWhiteSpace(item.Motivo))
            {
                var motivo = new TextView(this) { Text = item.Motivo };
                motivo.SetTextColor(Color.ParseColor("#C2414F"));
                motivo.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
                card.AddView(motivo);
            }

            if (!item.JaLancado)
            {
                var ajustar = new Button(this)
                {
                    Text = "Ajustar somente este mês"
                };
                ajustar.SetAllCaps(false);
                ajustar.Click += (s, e) => AbrirAjuste(item, check);
                card.AddView(ajustar);
            }

            destino.AddView(card);
            return check;
        }

        private string ObterRotulo(PreparacaoMensalItem item)
        {
            string descricao = EhReceita(item)
                ? $"{item.FontePagadora} · {item.Descricao}"
                : $"{item.Descricao} · {item.Categoria} · dia {item.DiaVencimento}";
            return $"{descricao}\nR$ {item.Valor.ToString("N2", _ptBr)}";
        }

        private void AbrirAjuste(PreparacaoMensalItem item, CheckBox check)
        {
            var form = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };
            form.SetPadding(32, 8, 32, 0);

            EditText AdicionarCampo(string hint, string? valor, InputTypes inputType = InputTypes.ClassText)
            {
                var campo = new EditText(this)
                {
                    Hint = hint,
                    Text = valor ?? string.Empty,
                    InputType = inputType
                };
                form.AddView(campo);
                return campo;
            }

            EditText? fonte = null;
            EditText? tipo = null;
            EditText? categoria = null;
            EditText? dia = null;
            if (EhReceita(item))
            {
                fonte = AdicionarCampo("Fonte pagadora", item.FontePagadora);
                tipo = AdicionarCampo("Tipo", item.Tipo);
            }
            else
            {
                categoria = AdicionarCampo("Categoria", item.Categoria);
                dia = AdicionarCampo("Dia do vencimento", item.DiaVencimento.ToString(CultureInfo.InvariantCulture), InputTypes.ClassNumber);
            }
            var descricao = AdicionarCampo("Descrição", item.Descricao);
            var valor = AdicionarCampo("Valor", null, InputTypes.ClassNumber);
            MoneyInputFormatter.Configure(valor);
            MoneyInputFormatter.SetValue(valor, item.Valor);

            var dialog = new AlertDialog.Builder(this)
                .SetTitle(EhReceita(item) ? "Ajustar receita" : "Ajustar despesa")
                .SetView(form)
                .SetNegativeButton("Cancelar", (s, e) => { })
                .SetPositiveButton("Aplicar", (s, e) =>
                {
                    decimal novoValor = MoneyInputFormatter.Parse(valor.Text);
                    if (novoValor <= 0)
                    {
                        Toast.MakeText(this, "Informe um valor válido.", ToastLength.Short)?.Show();
                        return;
                    }

                    item.Descricao = descricao.Text?.Trim() ?? string.Empty;
                    item.Valor = novoValor;
                    if (EhReceita(item))
                    {
                        item.FontePagadora = fonte?.Text?.Trim() ?? string.Empty;
                        item.Tipo = tipo?.Text?.Trim() ?? string.Empty;
                    }
                    else
                    {
                        item.Categoria = categoria?.Text?.Trim().ToUpperInvariant() ?? "OUTROS";
                        int novoDia = int.TryParse(dia?.Text, out int parsedDia) ? parsedDia : item.DiaVencimento;
                        item.DiaVencimento = Math.Clamp(novoDia, 1, 31);
                    }
                    item.Selecionado = true;
                    check.Checked = true;
                    AtualizarTextoItem(item, check);
                    AtualizarResumo();
                })
                .Create();
            dialog.Show();
        }

        private void AtualizarResumo()
        {
            var considerados = _itens.Where(x => x.JaLancado || x.Selecionado).ToList();
            decimal receitas = considerados.Where(EhReceita).Sum(x => x.Valor);
            decimal despesas = considerados.Where(x => !EhReceita(x)).Sum(x => x.Valor);
            int selecionados = _itens.Count(x => x.Selecionado && !x.JaLancado);
            int lancados = _itens.Count(x => x.JaLancado);
            _tvResumo.Text = $"Receitas: R$ {receitas.ToString("N2", _ptBr)}  ·  " +
                             $"Despesas: R$ {despesas.ToString("N2", _ptBr)}\n" +
                             $"Saldo previsto: R$ {(receitas - despesas).ToString("N2", _ptBr)}  ·  " +
                             $"{selecionados} a gerar, {lancados} já lançados";
            _btnGerar.Enabled = _itens.Any(x => !x.JaLancado);
        }

        private void ConfirmarGeracao()
        {
            int receitas = _itens.Count(x => EhReceita(x) && x.Selecionado && !x.JaLancado);
            int despesas = _itens.Count(x => !EhReceita(x) && x.Selecionado && !x.JaLancado);
            new AlertDialog.Builder(this)
                .SetTitle($"Preparar {_competencia:MM/yyyy}")
                .SetMessage($"Gerar {receitas} receitas e {despesas} despesas? Os itens desmarcados serão ignorados somente neste mês.")
                .SetNegativeButton("Cancelar", (s, e) => { })
                .SetPositiveButton("Gerar", async (s, e) => await GerarAsync())
                .Show();
        }

        private async Task GerarAsync()
        {
            _btnGerar.Enabled = false;
            try
            {
                var resultado = await _db.PrepararMesAsync(_competencia, _itens);
                await PersonalFinanceWidgetProvider.AtualizarTodosAsync(this);
                new AlertDialog.Builder(this)
                    .SetTitle("Mês preparado")
                    .SetMessage(
                        $"Competência {_competencia:MM/yyyy} atualizada.\n\n" +
                        $"Criados: {resultado.ReceitasCriadas} receitas e {resultado.DespesasCriadas} despesas.\n" +
                        $"Já existentes: {resultado.ReceitasExistentes} receitas e {resultado.DespesasExistentes} despesas.\n" +
                        $"Ignorados neste mês: {resultado.Ignorados}.")
                    .SetNegativeButton("Ver receitas", (s, e) => AbrirLista(typeof(ReceitaListActivity)))
                    .SetNeutralButton("Ver despesas", (s, e) => AbrirLista(typeof(DespesaListActivity)))
                    .SetPositiveButton("OK", async (s, e) => await CarregarAsync())
                    .Show();
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Erro ao preparar mês: {ex.Message}", ToastLength.Long)?.Show();
                _btnGerar.Enabled = true;
            }
        }

        private void AbrirLista(Type activityType)
        {
            var intent = new Intent(this, activityType);
            intent.PutExtra("Mes", _competencia.Month);
            intent.PutExtra("Ano", _competencia.Year);
            StartActivity(intent);
        }

        private static bool EhReceita(PreparacaoMensalItem item) =>
            string.Equals(item.TipoModelo, "RECEITA", StringComparison.OrdinalIgnoreCase);

        private static string ObterEstado(PreparacaoMensalItem item)
        {
            if (item.JaLancado)
                return " · JÁ LANÇADO";
            if (item.Ignorado)
                return item.Selecionado ? " · REATIVADO" : " · IGNORADO NESTE MÊS";
            if (item.AtualizacaoPendente)
                return " · ATUALIZAÇÃO PENDENTE";
            return string.Empty;
        }

        private void AtualizarTextoItem(PreparacaoMensalItem item, CheckBox check)
        {
            check.Text = ObterRotulo(item) + ObterEstado(item);
        }
    }
}
