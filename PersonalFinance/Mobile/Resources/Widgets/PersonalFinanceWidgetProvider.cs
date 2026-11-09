using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Android.Widget;
using PersonalFinance.Resources.Activities;
using PersonalFinance.Resources.Services;
using System.Globalization;

namespace PersonalFinance.Resources.Widgets;

[BroadcastReceiver(
    Enabled = true,
    Exported = true,
    Label = "Resumo de gastos pessoais")]
[IntentFilter([AppWidgetManager.ActionAppwidgetUpdate])]
[MetaData("android.appwidget.provider", Resource = "@xml/personal_finance_widget_info")]
public sealed class PersonalFinanceWidgetProvider : AppWidgetProvider
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public override void OnUpdate(
        Context? context,
        AppWidgetManager? appWidgetManager,
        int[]? appWidgetIds)
    {
        base.OnUpdate(context, appWidgetManager, appWidgetIds);

        if (context is null || appWidgetManager is null || appWidgetIds is null)
            return;

        ExecutarDuranteBroadcast(() => AtualizarAsync(context, appWidgetManager, appWidgetIds));
    }

    public override void OnAppWidgetOptionsChanged(
        Context? context,
        AppWidgetManager? appWidgetManager,
        int appWidgetId,
        Bundle? newOptions)
    {
        base.OnAppWidgetOptionsChanged(context, appWidgetManager, appWidgetId, newOptions);

        if (context is null || appWidgetManager is null)
            return;

        ExecutarDuranteBroadcast(() => AtualizarAsync(context, appWidgetManager, [appWidgetId]));
    }

    private void ExecutarDuranteBroadcast(Func<Task> atualizar)
    {
        var pendingResult = GoAsync();
        _ = ExecutarAsync();

        async Task ExecutarAsync()
        {
            try
            {
                await atualizar();
            }
            catch
            {
                // O proximo ciclo periodico tentara atualizar o widget novamente.
            }
            finally
            {
                pendingResult.Finish();
            }
        }
    }

    /// <summary>
    /// Atualiza todas as instâncias instaladas do widget. Operações que alterem
    /// despesas ou transações podem chamar este método depois de concluir a gravação.
    /// </summary>
    public static async Task AtualizarTodosAsync(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var manager = AppWidgetManager.GetInstance(context);
        var component = new ComponentName(
            context,
            Java.Lang.Class.FromType(typeof(PersonalFinanceWidgetProvider)));
        var ids = manager.GetAppWidgetIds(component);

        if (ids.Length > 0)
            await AtualizarAsync(context, manager, ids);
    }

    private static async Task AtualizarAsync(
        Context context,
        AppWidgetManager manager,
        IEnumerable<int> widgetIds)
    {
        var hoje = DateTime.Today;
        decimal orcamento = 0m;
        decimal realizado = 0m;
        decimal combustivelTotal = 0m;
        decimal combustivelPago = 0m;
        decimal casaTotal = 0m;
        decimal casaPaga = 0m;
        string? erro = null;
        DateTime? ultimaSincronizacao = null;

        try
        {
            var resumo = await new DatabaseService().ObterResumoPessoalAsync(hoje);
            orcamento = resumo.Orcamento;
            realizado = resumo.Realizado;
            combustivelTotal = resumo.CombustivelTotal;
            combustivelPago = resumo.CombustivelPago;
            casaTotal = resumo.CasaTotal;
            casaPaga = resumo.CasaPaga;
            ultimaSincronizacao = (await new DatabaseService().GetSyncStateAsync()).LastSyncUtc;
        }
        catch (Exception)
        {
            erro = "Não foi possível atualizar o resumo";
        }

        foreach (int widgetId in widgetIds)
        {
            var views = CriarViews(
                context,
                widgetId,
                hoje,
                orcamento,
                realizado,
                combustivelTotal,
                combustivelPago,
                casaTotal,
                casaPaga,
                erro,
                ultimaSincronizacao);
            manager.UpdateAppWidget(widgetId, views);
        }
    }

    private static RemoteViews CriarViews(
        Context context,
        int widgetId,
        DateTime competencia,
        decimal orcamento,
        decimal realizado,
        decimal combustivelTotal,
        decimal combustivelPago,
        decimal casaTotal,
        decimal casaPaga,
        string? erro,
        DateTime? ultimaSincronizacao)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_personal_finance);
        string mes = competencia.ToString("MMMM 'de' yyyy", PtBr);
        mes = PtBr.TextInfo.ToTitleCase(mes);

        views.SetTextViewText(Resource.Id.widgetCompetencia, mes);
        views.SetTextViewText(Resource.Id.widgetAtualizacao, ultimaSincronizacao.HasValue
            ? $"Cache: {ultimaSincronizacao.Value.ToLocalTime():dd/MM HH:mm}"
            : "Cache local · ainda não sincronizado");
        views.SetTextViewText(
            Resource.Id.widgetResumoValores,
            erro is null
                ? $"PESSOAL · {realizado.ToString("C", PtBr)} / {orcamento.ToString("C", PtBr)}"
                : "PESSOAL · --");
        views.SetTextViewText(
            Resource.Id.widgetResumoCategorias,
            erro is null
                ? $"COMBUSTÍVEL · {combustivelPago.ToString("C", PtBr)}"
                : "COMBUSTÍVEL · --");
        views.SetProgressBar(
            Resource.Id.widgetProgressoCombustivel,
            100,
            CalcularProgresso(combustivelTotal, combustivelPago),
            false);
        views.SetTextViewText(
            Resource.Id.widgetResumoCasa,
            erro is null
                ? $"CASA · {casaPaga.ToString("C", PtBr)}"
                : "CASA · --");
        views.SetProgressBar(
            Resource.Id.widgetProgressoCasa,
            100,
            CalcularProgresso(casaTotal, casaPaga),
            false);
        views.SetProgressBar(
            Resource.Id.widgetProgresso,
            100,
            CalcularProgresso(orcamento, realizado),
            false);

        var listaIntent = new Intent(context, typeof(TransacaoListActivity));
        listaIntent.SetFlags(ActivityFlags.ClearTop);
        listaIntent.PutExtra("Categoria", "PESSOAL");
        listaIntent.PutExtra("Mes", competencia.Month);
        listaIntent.PutExtra("Ano", competencia.Year);

        var gastoIntent = new Intent(context, typeof(QuickPersonalExpenseActivity));
        gastoIntent.SetFlags(ActivityFlags.ClearTop);

        views.SetOnClickPendingIntent(
            Resource.Id.widgetResumoArea,
            CriarPendingIntent(context, widgetId * 10 + 1, listaIntent));
        views.SetOnClickPendingIntent(
            Resource.Id.widgetLancarGasto,
            CriarPendingIntent(context, widgetId * 10 + 2, gastoIntent));

        return views;
    }

    private static PendingIntent CriarPendingIntent(Context context, int requestCode, Intent intent)
    {
        var flags = PendingIntentFlags.UpdateCurrent;

        if (OperatingSystem.IsAndroidVersionAtLeast(23))
            flags |= PendingIntentFlags.Immutable;

        return PendingIntent.GetActivity(context, requestCode, intent, flags)!;
    }

    private static int CalcularProgresso(decimal orcamento, decimal realizado)
    {
        if (orcamento <= 0m)
            return realizado > 0m ? 100 : 0;

        return (int)Math.Clamp(decimal.Round(realizado / orcamento * 100m), 0m, 100m);
    }

}
