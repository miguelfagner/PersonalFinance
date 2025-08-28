using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.Globalization;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Editar Transação")]
    public class TransacaoEditActivity : Activity
    {
        private EditText _edtData, _edtValor, _edtObservacao;
        private Button _btnSalvar, _btnExcluir;

        private DatabaseService _db;
        private Transacao _transacao;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (ActionBar != null)
                ActionBar.Hide();

            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.LightStatusBar;

            SetContentView(Resource.Layout.activity_transacao_edit);

            _db = new DatabaseService();

            // Obter ID da transação
            int transacaoId = Intent.GetIntExtra("TransacaoId", 0);

            if (transacaoId > 0)
                _transacao = await _db.PegarTransacaoAsync(transacaoId);

            if (_transacao == null)
            {
                // Nova transação
                _transacao = new Transacao { Data = DateTime.Today };
            }

            // Vincular componentes
            _edtData = FindViewById<EditText>(Resource.Id.edtData);
            _edtValor = FindViewById<EditText>(Resource.Id.edtValor);
            _edtObservacao = FindViewById<EditText>(Resource.Id.edtObservacao);
            _btnSalvar = FindViewById<Button>(Resource.Id.btnSalvarTransacao);
            _btnExcluir = FindViewById<Button>(Resource.Id.btnExcluirTransacao);

            // Preencher campos
            _edtData.Text = _transacao.Data.ToString("dd/MM/yyyy");
            _edtValor.Text = _transacao.Valor.ToString("F2", new CultureInfo("pt-BR"));
            _edtObservacao.Text = _transacao.Observacao;

            // DatePicker
            _edtData.Click += (s, e) =>
            {
                DateTime hoje = _transacao.Data != default ? _transacao.Data : DateTime.Today;

                var dialog = new DatePickerDialog(this, (sender, args) =>
                {
                    _edtData.Text = args.Date.ToString("dd/MM/yyyy");
                }, hoje.Year, hoje.Month - 1, hoje.Day);

                dialog.Show();
            };

            // Corrigir separador decimal
            _edtValor.TextChanged += (s, e) =>
            {
                if (_edtValor.Text.Contains("."))
                {
                    _edtValor.Text = _edtValor.Text.Replace(".", ",");
                    _edtValor.SetSelection(_edtValor.Text.Length);
                }
            };

            // Salvar
            _btnSalvar.Click += async (s, e) =>
            {
                try
                {
                    if (DateTime.TryParseExact(_edtData.Text, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime data))
                        _transacao.Data = data;

                    _transacao.Valor = decimal.TryParse(_edtValor.Text, NumberStyles.Any, new CultureInfo("pt-BR"), out decimal valor) ? valor : 0;
                    _transacao.Observacao = _edtObservacao.Text;

                    await _db.SalvarTransacaoAsync(_transacao);

                    Toast.MakeText(this, "Transação salva!", ToastLength.Short).Show();
                    Finish();
                }
                catch (Exception ex)
                {
                    Toast.MakeText(this, "Erro ao salvar: " + ex.Message, ToastLength.Long).Show();
                }
            };
