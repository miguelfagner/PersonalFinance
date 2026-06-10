using Android.Content;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(
        Label = "Controle Financeiro",
        MainLauncher = true,
        NoHistory = true,
        Theme = "@style/SplashTheme")]
    public class SplashActivity : Activity
    {
        protected override async void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_splash);

            var imagem = FindViewById<ImageView>(Resource.Id.imgSplash);
            var inicioExibicao = Task.Delay(TimeSpan.FromSeconds(5));

            var hoje = DateTime.Today;
            var inicioMes = new DateTime(hoje.Year, hoje.Month, 1);
            var fimMes = inicioMes.AddMonths(1).AddTicks(-1);

            var banco = new DatabaseService();
            var receitasTask = banco.ListaReceitasAsync(inicioMes, fimMes);
            var despesasTask = banco.ListaDespesasAsync(inicioMes, fimMes);

            await Task.WhenAll(receitasTask, despesasTask);

            var totalReceitas = receitasTask.Result.Sum(receita => receita.Valor);
            var totalDespesas = despesasTask.Result.Sum(despesa => despesa.Valor);

            imagem?.SetImageResource(
                totalDespesas < totalReceitas
                    ? Resource.Drawable.splash_rico
                    : Resource.Drawable.splash_apertado);

            await inicioExibicao;

            StartActivity(new Intent(this, typeof(MainActivity)));
            Finish();
        }
    }
}
