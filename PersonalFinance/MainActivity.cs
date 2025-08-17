using Android.Content;
using PersonalFinance.Resources.Activities;

namespace PersonalFinance
{

    [Activity(Label = "Controle Financeiro", MainLauncher = true)]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            var btnReceitaLista = FindViewById<Button>(Resource.Id.btnListarReceitas);
            var btnDespesaLista = FindViewById<Button>(Resource.Id.btnListarDespesas);

            //btnDespesa.Click += (s, e) =>
            //{
            //    var intent = new Intent(this, typeof(DespesaCreateActivity));
            //    intent.PutExtra("tipo", "Despesa");
            //    StartActivity(intent);
            //};

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
        }
    }

}