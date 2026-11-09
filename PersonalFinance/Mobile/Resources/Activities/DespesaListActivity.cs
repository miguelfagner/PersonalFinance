using Android.Content;
using PersonalFinance.Resources.Adapters;

namespace PersonalFinance.Resources.Activities;

[Activity(Label = "Despesas")]
public class DespesaListActivity : ListaMensalActivity
{
    protected override int LayoutLista => Resource.Layout.activity_despesa_list;
    protected override int IdLista => Resource.Id.listDespesas;
    protected override string TituloLista => "Despesas";
    protected override int MensagemSemLancamentos => Resource.String.lista_mensal_sem_despesas;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        Lista.ItemClick += (_, e) =>
        {
            if (Lista.Adapter is not DespesaAdapter adapter || e.Position < 0 || e.Position >= adapter.Count)
                return;

            var intent = new Intent(this, typeof(DespesaEditActivity));
            intent.PutExtra("DespesaId", adapter[e.Position].Id);
            StartActivity(intent);
        };

        FindViewById<Button>(Resource.Id.btnAddDespesa)!.Click += (_, _) =>
        {
            var intent = new Intent(this, typeof(DespesaCreateActivity));
            intent.PutExtra("Mes", Competencia.Month);
            intent.PutExtra("Ano", Competencia.Year);
            StartActivity(intent);
        };
    }

    protected override async Task<IListAdapter> CarregarAdapterAsync(DateTime inicio, DateTime fim)
    {
        var itens = await Database.ListaDespesasAsync(inicio, fim);
        return new DespesaAdapter(this, itens);
    }
}
