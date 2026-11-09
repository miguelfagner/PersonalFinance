using Android.Graphics;
using Android.Views;
using PersonalFinance.Resources.Helpers;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.Globalization;

namespace PersonalFinance.Resources.Activities
{
    public abstract class DespesaFixaFormActivityBase : FinanceActivity
    {
        private FinanceRepository _db = null!;
        private DespesaFixa _item = null!;
        private List<ReceitaFixa> _receitasDisponiveis = new();
        private Spinner _receita = null!;
        private Spinner _categoria = null!;
        private EditText _descricao = null!;
        private EditText _valor = null!;
        private EditText _diaVencimento = null!;
        private TextView _titulo = null!;
        private TextView _subtitulo = null!;
        private TextView _status = null!;
        private Button _salvar = null!;
        private Button _pausar = null!;
        private Button _excluir = null!;

        protected abstract bool Editando { get; }

        protected override async void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_despesa_fixa_form);

            _db = new FinanceRepository();
            VincularControles();
            MoneyInputFormatter.Configure(_valor);

            if (Editando)
            {
                int id = Intent.GetIntExtra(DespesaFixaEditActivity.ExtraDespesaFixaId, 0);
                if (id <= 0 || !await CarregarAsync(id))
                    return;
            }
            else
            {
                _item = new DespesaFixa
                {
                    Ativa = true,
                    DiaVencimento = Math.Clamp(DateTime.Today.Day, 1, 31)
                };
                MoneyInputFormatter.SetValue(_valor, 0m);
                _diaVencimento.Text = _item.DiaVencimento.ToString(CultureInfo.InvariantCulture);
            }

            await CarregarOpcoesAsync();
            ConfigurarTela();
            _salvar.Click += async (s, e) => await SalvarAsync();
            _pausar.Click += (s, e) => ConfirmarPausa();
            _excluir.Click += (s, e) => ConfirmarExclusao();
        }

        private void VincularControles()
        {
            _receita = FindViewById<Spinner>(Resource.Id.spinnerDespesaFixaReceita)!;
            _categoria = FindViewById<Spinner>(Resource.Id.spinnerDespesaFixaCategoria)!;
            _descricao = FindViewById<EditText>(Resource.Id.edtDespesaFixaDescricao)!;
            _valor = FindViewById<EditText>(Resource.Id.edtDespesaFixaValor)!;
            _diaVencimento = FindViewById<EditText>(Resource.Id.edtDespesaFixaDiaVencimento)!;
            _titulo = FindViewById<TextView>(Resource.Id.tvDespesaFixaFormTitulo)!;
            _subtitulo = FindViewById<TextView>(Resource.Id.tvDespesaFixaFormSubtitulo)!;
            _status = FindViewById<TextView>(Resource.Id.tvDespesaFixaFormStatus)!;
            _salvar = FindViewById<Button>(Resource.Id.btnSalvarDespesaFixa)!;
            _pausar = FindViewById<Button>(Resource.Id.btnPausarDespesaFixa)!;
            _excluir = FindViewById<Button>(Resource.Id.btnExcluirDespesaFixa)!;
        }

        private async Task<bool> CarregarAsync(int id)
        {
            try
            {
                var item = await _db.PegarDespesaFixaAsync(id);
                if (item == null)
                {
                    Toast.MakeText(this, "Despesa fixa não encontrada.", ToastLength.Long)!.Show();
                    Finish();
                    return false;
                }

                _item = item;
                _descricao.Text = item.Descricao;
                _diaVencimento.Text = item.DiaVencimento.ToString(CultureInfo.InvariantCulture);
                MoneyInputFormatter.SetValue(_valor, item.Valor);
                return true;
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Não foi possível carregar a despesa fixa: {ex.Message}", ToastLength.Long)!.Show();
                Finish();
                return false;
            }
        }

        private async Task CarregarOpcoesAsync()
        {
            try
            {
                var todas = await _db.ListarReceitasFixasAsync(incluirInativas: true);
                _receitasDisponiveis = todas
                    .Where(x => x.Ativa || (Editando && x.Id == _item.ReceitaFixaId))
                    .OrderByDescending(x => x.Ativa)
                    .ThenBy(x => x.FontePagadora)
                    .ToList();

                IEnumerable<string> nomesReceitas = _receitasDisponiveis.Count == 0
                    ? new[] { "Cadastre uma receita fixa primeiro" }
                    : _receitasDisponiveis.Select(x => x.Ativa
                        ? x.FontePagadora
                        : $"{x.FontePagadora} (pausada)");
                _receita.Adapter = FormOptions.CreateSpinnerAdapter(this, nomesReceitas);

                int receitaIndex = _receitasDisponiveis.FindIndex(x => x.Id == _item.ReceitaFixaId);
                if (receitaIndex >= 0)
                    _receita.SetSelection(receitaIndex);

                var categorias = FormOptions.WithCurrentOption(FormOptions.CategoriasDespesa, _item.Categoria);
                _categoria.Adapter = FormOptions.CreateSpinnerAdapter(this, categorias);

                int categoriaIndex = categorias.FindIndex(x =>
                    string.Equals(x, _item.Categoria, StringComparison.OrdinalIgnoreCase));
                if (categoriaIndex >= 0)
                    _categoria.SetSelection(categoriaIndex);

                if (_receitasDisponiveis.Count == 0)
                {
                    _salvar.Enabled = false;
                    Toast.MakeText(this, "Cadastre uma receita fixa ativa antes de criar uma despesa fixa.", ToastLength.Long)!.Show();
                }
            }
            catch (Exception ex)
            {
                _salvar.Enabled = false;
                Toast.MakeText(this, $"Não foi possível carregar as receitas fixas: {ex.Message}", ToastLength.Long)!.Show();
            }
        }

        private void ConfigurarTela()
        {
            if (!Editando)
                return;

            _titulo.Text = "Editar despesa fixa";
            _subtitulo.Text = "As alterações valerão somente para as próximas competências preparadas.";
            _status.Visibility = ViewStates.Visible;
            _pausar.Visibility = ViewStates.Visible;
            _excluir.Visibility = ViewStates.Visible;
            AtualizarStatus();
        }

        private void AtualizarStatus()
        {
            _status.Text = _item.Ativa ? "ATIVA" : "PAUSADA";
            _status.SetTextColor(Color.ParseColor(_item.Ativa ? "#18794E" : "#B7791F"));
            _pausar.Text = _item.Ativa ? "Pausar modelo" : "Reativar modelo";
        }

        private async Task SalvarAsync()
        {
            int receitaIndex = _receita.SelectedItemPosition;
            string descricao = _descricao.Text?.Trim() ?? string.Empty;
            string categoria = _categoria.SelectedItem?.ToString()?.Trim() ?? string.Empty;
            decimal valor = MoneyInputFormatter.Parse(_valor.Text);

            if (receitaIndex < 0 || receitaIndex >= _receitasDisponiveis.Count)
            {
                Toast.MakeText(this, "Selecione uma receita fixa.", ToastLength.Short)!.Show();
                return;
            }

            if (string.IsNullOrWhiteSpace(descricao))
            {
                MostrarErro(_descricao, "Informe a descrição da despesa.");
                return;
            }

            if (string.IsNullOrWhiteSpace(categoria))
            {
                Toast.MakeText(this, "Selecione uma categoria.", ToastLength.Short)!.Show();
                return;
            }

            if (valor <= 0m)
            {
                MostrarErro(_valor, "Informe um valor maior que zero.");
                return;
            }

            if (!int.TryParse(_diaVencimento.Text, out int dia) || dia < 1 || dia > 31)
            {
                MostrarErro(_diaVencimento, "Informe um dia de vencimento entre 1 e 31.");
                return;
            }

            _item.ReceitaFixaId = _receitasDisponiveis[receitaIndex].Id;
            _item.Descricao = descricao;
            _item.Categoria = categoria.ToUpperInvariant();
            _item.Valor = valor;
            _item.DiaVencimento = dia;

            _salvar.Enabled = false;
            try
            {
                await _db.SalvarDespesaFixaAsync(_item);
                Toast.MakeText(this, Editando ? "Despesa fixa atualizada." : "Despesa fixa cadastrada.", ToastLength.Short)!.Show();
                Finish();
            }
            catch (InvalidOperationException ex)
            {
                Toast.MakeText(this, ex.Message, ToastLength.Long)!.Show();
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Não foi possível salvar a despesa fixa: {ex.Message}", ToastLength.Long)!.Show();
            }
            finally
            {
                _salvar.Enabled = true;
            }
        }

        private void ConfirmarPausa()
        {
            bool pausar = _item.Ativa;
            new AlertDialog.Builder(this)
                .SetTitle(pausar ? "Pausar despesa fixa" : "Reativar despesa fixa")
                .SetMessage(pausar
                    ? "Esta despesa deixará de aparecer nas próximas preparações. Os meses já preparados não serão alterados."
                    : "Esta despesa voltará a aparecer nas próximas preparações quando sua receita vinculada estiver ativa.")
                .SetPositiveButton(pausar ? "Pausar" : "Reativar", async (s, e) => await AlterarPausaAsync(pausar))
                .SetNegativeButton("Cancelar", (s, e) => { })
                .Show();
        }

        private async Task AlterarPausaAsync(bool pausar)
        {
            _pausar.Enabled = false;
            try
            {
                await _db.PausarDespesaFixaAsync(_item.Id, pausar);
                _item.Ativa = !pausar;
                AtualizarStatus();
                Toast.MakeText(this, pausar ? "Despesa fixa pausada." : "Despesa fixa reativada.", ToastLength.Short)!.Show();
            }
            catch (InvalidOperationException ex)
            {
                Toast.MakeText(this, ex.Message, ToastLength.Long)!.Show();
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Não foi possível alterar o estado: {ex.Message}", ToastLength.Long)!.Show();
            }
            finally
            {
                _pausar.Enabled = true;
            }
        }

        private void ConfirmarExclusao()
        {
            new AlertDialog.Builder(this)
                .SetTitle("Excluir despesa fixa")
                .SetMessage("A exclusão não altera lançamentos mensais já criados. Deseja continuar?")
                .SetPositiveButton("Excluir", async (s, e) => await ExcluirAsync())
                .SetNegativeButton("Cancelar", (s, e) => { })
                .Show();
        }

        private async Task ExcluirAsync()
        {
            _excluir.Enabled = false;
            try
            {
                await _db.ExcluirDespesaFixaAsync(_item.Id);
                Toast.MakeText(this, "Despesa fixa excluída.", ToastLength.Short)!.Show();
                Finish();
            }
            catch (InvalidOperationException ex)
            {
                Toast.MakeText(this, ex.Message, ToastLength.Long)!.Show();
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Não foi possível excluir a despesa fixa: {ex.Message}", ToastLength.Long)!.Show();
            }
            finally
            {
                _excluir.Enabled = true;
            }
        }

        private void MostrarErro(EditText campo, string mensagem)
        {
            campo.RequestFocus();
            campo.Error = mensagem;
            Toast.MakeText(this, mensagem, ToastLength.Short)!.Show();
        }
    }

    [Activity(Label = "Nova Despesa Fixa")]
    public sealed class DespesaFixaCreateActivity : DespesaFixaFormActivityBase
    {
        protected override bool Editando => false;
    }

    [Activity(Label = "Editar Despesa Fixa")]
    public sealed class DespesaFixaEditActivity : DespesaFixaFormActivityBase
    {
        public const string ExtraDespesaFixaId = "DespesaFixaId";
        protected override bool Editando => true;
    }
}
