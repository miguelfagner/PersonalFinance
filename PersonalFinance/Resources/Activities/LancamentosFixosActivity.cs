using Android.Content;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Lançamentos Fixos")]
    public class LancamentosFixosActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_lancamentos_fixos);

            FindViewById<Button>(Resource.Id.btnGerenciarReceitasFixas)!.Click += (s, e) =>
                StartActivity(new Intent(this, typeof(ReceitaFixaListActivity)));

            FindViewById<Button>(Resource.Id.btnGerenciarDespesasFixas)!.Click += (s, e) =>
                StartActivity(new Intent(this, typeof(DespesaFixaListActivity)));

            FindViewById<Button>(Resource.Id.btnImportarLancamentosFixos)!.Click += (s, e) =>
                StartActivity(new Intent(this, typeof(ImportarLancamentosFixosActivity)));
        }
    }
}
