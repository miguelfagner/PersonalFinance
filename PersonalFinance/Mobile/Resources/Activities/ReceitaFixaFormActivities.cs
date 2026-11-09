using Android.Graphics;
using Android.Views;
using PersonalFinance.Resources.Helpers;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    public abstract class ReceitaFixaFormActivityBase : FinanceActivity
    {
        private FinanceRepository _db = null!;
        private ReceitaFixa _item = null!;
        private EditText _fonte = null!;
        private EditText _descricao = null!;
        private EditText _tipo = null!;
        private EditText _valor = null!;
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
            SetContentView(Resource.Layout.activity_receita_fixa_form);

            _db = new FinanceRepository();
            VincularControles();
            MoneyInputFormatter.Configure(_valor);

            if (Editando)
            {
                int id = Intent.GetIntExtra(ReceitaFixaEditActivity.ExtraReceitaFixaId, 0);
                if (id <= 0 || !await CarregarAsync(id))
                    return;
            }
            else
            {
                _item = new ReceitaFixa { Ativa = true };
                MoneyInputFormatter.SetValue(_valor, 0m);
            }

            ConfigurarTela();
            _salvar.Click += async (s, e) => await SalvarAsync();
            _pausar.Click += (s, e) => ConfirmarPausa();
            _excluir.Click += (s, e) => ConfirmarExclusao();
        }

        private void VincularControles()
        {
            _fonte = FindViewById<EditText>(Resource.Id.edtReceitaFixaFonte)!;
            _descricao = FindViewById<EditText>(Resource.Id.edtReceitaFixaDescricao)!;
            _tipo = FindViewById<EditText>(Resource.Id.edtReceitaFixaTipo)!;
            _valor = FindViewById<EditText>(Resource.Id.edtReceitaFixaValor)!;
            _titulo = FindViewById<TextView>(Resource.Id.tvReceitaFixaFormTitulo)!;
            _subtitulo = FindViewById<TextView>(Resource.Id.tvReceitaFixaFormSubtitulo)!;
            _status = FindViewById<TextView>(Resource.Id.tvReceitaFixaFormStatus)!;
            _salvar = FindViewById<Button>(Resource.Id.btnSalvarReceitaFixa)!;
            _pausar = FindViewById<Button>(Resource.Id.btnPausarReceitaFixa)!;
            _excluir = FindViewById<Button>(Resource.Id.btnExcluirReceitaFixa)!;
        }

        private async Task<bool> CarregarAsync(int id)
        {
            try
            {
                var item = await _db.PegarReceitaFixaAsync(id);
                if (item == null)
                {
                    Toast.MakeText(this, "Receita fixa não encontrada.", ToastLength.Long)!.Show();
                    Finish();
                    return false;
                }

                _item = item;
                _fonte.Text = item.FontePagadora;
                _descricao.Text = item.Descricao;
                _tipo.Text = item.Tipo;
                MoneyInputFormatter.SetValue(_valor, item.Valor);
                return true;
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Não foi possível carregar a receita fixa: {ex.Message}", ToastLength.Long)!.Show();
                Finish();
                return false;
            }
        }

        private void ConfigurarTela()
        {
            if (!Editando)
                return;

            _titulo.Text = "Editar receita fixa";
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
            string fonte = _fonte.Text?.Trim() ?? string.Empty;
            string descricao = _descricao.Text?.Trim() ?? string.Empty;
            string tipo = _tipo.Text?.Trim() ?? string.Empty;
            decimal valor = MoneyInputFormatter.Parse(_valor.Text);

            if (string.IsNullOrWhiteSpace(fonte))
            {
                MostrarErro(_fonte, "Informe a fonte pagadora.");
                return;
            }

            if (string.IsNullOrWhiteSpace(descricao))
            {
                MostrarErro(_descricao, "Informe a descrição da receita.");
                return;
            }

            if (string.IsNullOrWhiteSpace(tipo))
            {
                MostrarErro(_tipo, "Informe o tipo da receita.");
                return;
            }

            if (valor <= 0m)
            {
                MostrarErro(_valor, "Informe um valor maior que zero.");
                return;
            }

            _item.FontePagadora = fonte;
            _item.Descricao = descricao;
            _item.Tipo = tipo;
            _item.Valor = valor;

            _salvar.Enabled = false;
            try
            {
                await _db.SalvarReceitaFixaAsync(_item);
                Toast.MakeText(this, Editando ? "Receita fixa atualizada." : "Receita fixa cadastrada.", ToastLength.Short)!.Show();
                Finish();
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Não foi possível salvar a receita fixa: {ex.Message}", ToastLength.Long)!.Show();
            }
            finally
            {
                _salvar.Enabled = true;
            }
        }

        private void ConfirmarPausa()
        {
            bool pausar = _item.Ativa;
            string mensagem = pausar
                ? "Ao pausar esta receita, as despesas fixas vinculadas também serão pausadas. Os lançamentos mensais já criados não serão alterados."
                : "A receita será reativada. As despesas vinculadas continuarão pausadas até que você as reative individualmente.";

            new AlertDialog.Builder(this)
                .SetTitle(pausar ? "Pausar receita fixa" : "Reativar receita fixa")
                .SetMessage(mensagem)
                .SetPositiveButton(pausar ? "Pausar" : "Reativar", async (s, e) => await AlterarPausaAsync(pausar))
                .SetNegativeButton("Cancelar", (s, e) => { })
                .Show();
        }

        private async Task AlterarPausaAsync(bool pausar)
        {
            _pausar.Enabled = false;
            try
            {
                await _db.PausarReceitaFixaAsync(_item.Id, pausar);
                _item.Ativa = !pausar;
                AtualizarStatus();
                Toast.MakeText(this, pausar ? "Receita fixa pausada." : "Receita fixa reativada.", ToastLength.Short)!.Show();
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
                .SetTitle("Excluir receita fixa")
                .SetMessage("A exclusão só será permitida se não houver despesas fixas vinculadas. Os lançamentos mensais já criados serão preservados. Deseja continuar?")
                .SetPositiveButton("Excluir", async (s, e) => await ExcluirAsync())
                .SetNegativeButton("Cancelar", (s, e) => { })
                .Show();
        }

        private async Task ExcluirAsync()
        {
            _excluir.Enabled = false;
            try
            {
                await _db.ExcluirReceitaFixaAsync(_item.Id);
                Toast.MakeText(this, "Receita fixa excluída.", ToastLength.Short)!.Show();
                Finish();
            }
            catch (InvalidOperationException ex)
            {
                Toast.MakeText(this, ex.Message, ToastLength.Long)!.Show();
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Não foi possível excluir a receita fixa: {ex.Message}", ToastLength.Long)!.Show();
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

    [Activity(Label = "Nova Receita Fixa")]
    public sealed class ReceitaFixaCreateActivity : ReceitaFixaFormActivityBase
    {
        protected override bool Editando => false;
    }

    [Activity(Label = "Editar Receita Fixa")]
    public sealed class ReceitaFixaEditActivity : ReceitaFixaFormActivityBase
    {
        public const string ExtraReceitaFixaId = "ReceitaFixaId";
        protected override bool Editando => true;
    }
}
