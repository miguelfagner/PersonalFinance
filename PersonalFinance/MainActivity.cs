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

            var btnReceita = FindViewById<Button>(Resource.Id.btnAdicionarReceita);
            var btnDespesa = FindViewById<Button>(Resource.Id.btnAdicionarDespesa);
            var btnReceitaLista = FindViewById<Button>(Resource.Id.btnListarReceitas);
            var btnDespesaLista = FindViewById<Button>(Resource.Id.btnListarDespesas);

            btnReceita.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(ReceitaCadastroActivity));
                intent.PutExtra("tipo", "Receita");
                StartActivity(intent);
            };

            btnDespesa.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(DespesaCadastroActivity));
                intent.PutExtra("tipo", "Despesa");
                StartActivity(intent);
            };

            //btnListar.Click += (s, e) =>
            //{
            //    var intent = new Intent(this, typeof(ExtratoActivity));
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