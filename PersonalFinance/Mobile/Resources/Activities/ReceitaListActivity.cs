using Android.Content;
using PersonalFinance.Resources.Adapters;

namespace PersonalFinance.Resources.Activities;

[Activity(Label = "Lista de Receitas")]
public class ReceitaListActivity : ListaMensalActivity
{
    protected override int LayoutLista => Resource.Layout.activity_receita_list;
    protected override int IdLista => Resource.Id.listReceitas;
    protected override string TituloLista => "Receitas";
    protected override int MensagemSemLancamentos => Resource.String.lista_mensal_sem_receitas;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        Lista.ItemClick += (_, e) =>
        {
            if (Lista.Adapter is not ReceitaAdapter adapter || e.Position < 0 || e.Position >= adapter.Count)
                return;

            var intent = new Intent(this, typeof(ReceitaEditActivity));
            intent.PutExtra("ReceitaId", adapter[e.Position].Id);
            StartActivity(intent);
        };

        FindViewById<Button>(Resource.Id.btnAddReceita)!.Click += (_, _) =>
        {
            var intent = new Intent(this, typeof(ReceitaCreateActivity));
            intent.PutExtra("tipo", "Receita");
            intent.PutExtra("Mes", Competencia.Month);
            intent.PutExtra("Ano", Competencia.Year);
            StartActivity(intent);
        };
    }

    protected override async Task<IListAdapter> CarregarAdapterAsync(DateTime inicio, DateTime fim)
    {
        var itens = await Database.ListaReceitasAsync(inicio, fim);
        return new ReceitaAdapter(this, itens);
    }
}
