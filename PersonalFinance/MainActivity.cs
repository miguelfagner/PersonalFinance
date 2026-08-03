using Android.Content;
using PersonalFinance.Resources.Activities;

namespace PersonalFinance
{

    [Activity(Label = "Controle Financeiro")]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            var btnReceitaLista = FindViewById<Button>(Resource.Id.btnListarReceitas);
            var btnDespesaLista = FindViewById<Button>(Resource.Id.btnListarDespesas);
            var btnTransacaoLista = FindViewById<Button>(Resource.Id.btnListarTransacoes);
            var btnBalanco = FindViewById<Button>(Resource.Id.btnBalanco);
            var btnValoresAQuitar = FindViewById<Button>(Resource.Id.btnValoresAQuitar);
            var btnExportar = FindViewById<Button>(Resource.Id.btnExportarBackup);
            var btnImportar = FindViewById<Button>(Resource.Id.btnImportarBackup);
            var btnPrepararProximoMes = FindViewById<Button>(Resource.Id.btnPrepararProximoMes);
            var btnLancamentosFixos = FindViewById<Button>(Resource.Id.btnLancamentosFixos);


            btnExportar.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(BackupExportActivity));
                StartActivity(intent);
            };

            btnImportar.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(BackupImportActivity));
                StartActivity(intent);
            };

            btnPrepararProximoMes.Click += (s, e) =>
            {
                var proximoMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
                    .AddMonths(1);
                var seletor = new DatePickerDialog(
                    this,
                    (sender, args) =>
                    {
                        var intent = new Intent(this, typeof(PrepararMesActivity));
                        intent.PutExtra("Mes", args.Date.Month);
                        intent.PutExtra("Ano", args.Date.Year);
                        StartActivity(intent);
                    },
                    proximoMes.Year,
                    proximoMes.Month - 1,
                    1);
                seletor.SetTitle("Selecione a competência");
                seletor.Show();
            };

            btnLancamentosFixos.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(LancamentosFixosActivity));
                StartActivity(intent);
            };

            btnValoresAQuitar.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(DespesaEmAbertoListActivity));
                StartActivity(intent);
            };

            var btnListaReceitas = FindViewById<Button>(Resource.Id.btnListarReceitas);
            btnReceitaLista.Click += (s, e) =>
            {
                var intent = new Android.Content.Intent(this, typeof(ReceitaListActivity));
                StartActivity(intent);
            };

            var btnListaDespesas = FindViewById<Button>(Resource.Id.btnListarDespesas);
            btnDespesaLista.Click += (s, e) =>
            {
                var intent = new Android.Content.Intent(this, typeof(DespesaListActivity));
                StartActivity(intent);
            };

            var btnListaTransacoes = FindViewById<Button>(Resource.Id.btnListarTransacoes);
            btnTransacaoLista.Click += (s, e) =>
            {
                var intent = new Android.Content.Intent(this, typeof(TransacaoListActivity));
                StartActivity(intent);
            };

            var btnBalancoDados = FindViewById<Button>(Resource.Id.btnBalanco);
            btnBalanco.Click += (s, e) =>
            {
                var intent = new Android.Content.Intent(this, typeof(BalancoDetailActivity));
                StartActivity(intent);
            };
        }
    }

}
