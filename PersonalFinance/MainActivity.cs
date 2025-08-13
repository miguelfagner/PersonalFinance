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
            var btnListar = FindViewById<Button>(Resource.Id.btnListar);

            btnReceita.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(CadastroReceitaActivity));
                intent.PutExtra("tipo", "Receita");
                StartActivity(intent);
            };

            btnDespesa.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(CadastroDespesaActivity));
                intent.PutExtra("tipo", "Despesa");
                StartActivity(intent);
            };

            btnListar.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(ListagemActivity));
                StartActivity(intent);
            };
        }
    }

}