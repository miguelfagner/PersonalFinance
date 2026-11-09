using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using PersonalFinance.Resources.Helpers;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System;
using System.Globalization;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Editar Receita")]
    public class ReceitaEditActivity : FinanceActivity
    {
        private EditText _edtMesReferencia, _edtFonte, _edtDescricao, _edtTipo, _edtValor;
        private Button _btnSalvar, _btnExcluir;

        private FinanceRepository _db;
        private Receita _receita;
        private DateTime _mesSelecionado;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Define a cor da StatusBar
            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.LightStatusBar; // ícones escuros

            SetContentView(Resource.Layout.activity_receita_edit);

            // Inicializar DB
            _db = new FinanceRepository();

            // Obter ID da receita que veio da lista
            int receitaId = Intent.GetIntExtra("ReceitaId", 0);

            if (receitaId == 0)
            {
                Toast.MakeText(this, "Erro ao carregar receita.", ToastLength.Short).Show();
                Finish();
                return;
            }

            // Carregar receita
            _receita = await _db.PegarReceitaAsync(receitaId);

            if (_receita == null)
            {
                Toast.MakeText(this, "Receita não encontrada.", ToastLength.Short).Show();
                Finish();
                return;
            }

            // Vincular componentes
            _edtMesReferencia = FindViewById<EditText>(Resource.Id.edtMesReferenciaEdit);
            _edtFonte = FindViewById<EditText>(Resource.Id.edtFonteEdit);
            _edtDescricao = FindViewById<EditText>(Resource.Id.edtDescricaoEdit);
            _edtTipo = FindViewById<EditText>(Resource.Id.edtTipoEdit);
            _edtValor = FindViewById<EditText>(Resource.Id.edtValorEdit)!;
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvarEdit);
            _btnExcluir = FindViewById<Button>(Resource.Id.btnExcluirReceita);
            MoneyInputFormatter.Configure(_edtValor);

            // Preencher campos
            _mesSelecionado = _receita.MesReferencia == default
                ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
                : new DateTime(_receita.MesReferencia.Year, _receita.MesReferencia.Month, 1);

            _edtMesReferencia.Text = _mesSelecionado.ToString("MM/yyyy", CultureInfo.InvariantCulture);
            _edtFonte.Text = _receita.FontePagadora;
            _edtDescricao.Text = _receita.Descricao;
            _edtTipo.Text = _receita.Tipo;
            MoneyInputFormatter.SetValue(_edtValor, _receita.Valor);

            if (_receita.CompetenciaGeracao.HasValue)
            {
                _edtMesReferencia.Enabled = false;
                _edtMesReferencia.Alpha = 0.65f;
            }

            if (!_receita.CompetenciaGeracao.HasValue)
            {
                _edtMesReferencia.Click += (s, e) =>
                {
                    var dialog = new DatePickerDialog(this,
                        (sender, args) =>
                        {
                            _mesSelecionado = new DateTime(args.Date.Year, args.Date.Month, 1);
                            _edtMesReferencia.Text = _mesSelecionado.ToString("MM/yyyy");
                        },
                        _mesSelecionado.Year,
                        _mesSelecionado.Month - 1,
                        1);

                    dialog.Show();
                };
            }


            // Clique no botão Salvar
            _btnSalvar.Click += async (s, e) =>
            {
                try
                {
                    _receita.MesReferencia = _mesSelecionado;
                    _receita.FontePagadora = _edtFonte.Text;
                    _receita.Descricao = _edtDescricao.Text;
                    _receita.Tipo = _edtTipo.Text;
                    _receita.Valor = MoneyInputFormatter.Parse(_edtValor.Text);

                    await _db.SalvarReceitaAsync(_receita);

                    Toast.MakeText(this, "Receita atualizada!", ToastLength.Short).Show();
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
                    .SetTitle("Excluir Receita")
                    .SetMessage("Tem certeza que deseja excluir esta receita?")
                    .SetPositiveButton("Sim", async (senderAlert, args) =>
                    {
                        try
                        {
                            await _db.DeletarReceitaAsync(_receita);
                            Toast.MakeText(this, "Receita excluída!", ToastLength.Short).Show();
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
    }
}
