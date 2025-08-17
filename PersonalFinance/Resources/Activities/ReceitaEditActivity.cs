using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System;
using System.Globalization;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Editar Receita")]
    public class ReceitaEditActivity : Activity
    {
        private EditText _edtMesReferencia, _edtFonte, _edtDescricao, _edtTipo, _edtValor;
        private Button _btnSalvar, _btnExcluir;

        private DatabaseService _db;
        private Receita _receita;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (ActionBar != null)
            {
                ActionBar.Hide();
            }

            // Define a cor da StatusBar
            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.LightStatusBar; // ícones escuros

            SetContentView(Resource.Layout.activity_receita_edit);

            // Inicializar DB
            _db = new DatabaseService();

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
            _edtValor = FindViewById<EditText>(Resource.Id.edtValorEdit);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvarEdit);
            _btnExcluir = FindViewById<Button>(Resource.Id.btnExcluirReceita);

            // Preencher campos
            _edtMesReferencia.Text = _receita.MesReferencia.ToString("MM/yyyy");
            _edtFonte.Text = _receita.FontePagadora;
            _edtDescricao.Text = _receita.Descricao;
            _edtTipo.Text = _receita.Tipo;
            _edtValor.Text = _receita.Valor.ToString("F2", new CultureInfo("pt-BR"));


            // ajustar o valor decimal
            _edtValor.TextChanged += (s, e) =>
            {
                if (_edtValor.Text.Contains("."))
                {
                    _edtValor.Text = _edtValor.Text.Replace(".", ",");
                    _edtValor.SetSelection(_edtValor.Text.Length); // mantém cursor no fim
                }
            };

            // Clique no botão Salvar
            _btnSalvar.Click += async (s, e) =>
            {
                try
                {
                    _receita.FontePagadora = _edtFonte.Text;
                    _receita.Descricao = _edtDescricao.Text;
                    _receita.Tipo = _edtTipo.Text;
                    _receita.Valor = decimal.TryParse(_edtValor.Text, out decimal valor) ? valor : 0;

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
