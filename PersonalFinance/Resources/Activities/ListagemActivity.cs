using Android.Views;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{

    [Activity(Label = "Listagem")]
    public class ListagemActivity : Activity
    {
        DatabaseService _db;
        LinearLayout _layoutReceitas, _layoutDespesas;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_listagem);

            _db = new DatabaseService();

            _layoutReceitas = FindViewById<LinearLayout>(Resource.Id.layoutReceitas);
            _layoutDespesas = FindViewById<LinearLayout>(Resource.Id.layoutDespesas);

            var receitas = (await _db.GetTransacoesAsync())
                .Where(t => t.Tipo == "Receita")
                .OrderByDescending(t => t.Valor)
                .ToList();

            var despesas = (await _db.GetTransacoesAsync())
                .Where(t => t.Tipo == "Despesa")
                .OrderByDescending(t => t.Valor)
                .ToList();

            PopularLista(_layoutReceitas, receitas);
            PopularLista(_layoutDespesas, despesas);
        }

        protected override async void OnResume()
        {
            base.OnResume();
            //SetContentView(Resource.Layout.activity_listagem);

            _db = new DatabaseService();

            _layoutReceitas = FindViewById<LinearLayout>(Resource.Id.layoutReceitas);
            _layoutDespesas = FindViewById<LinearLayout>(Resource.Id.layoutDespesas);

            var receitas = (await _db.GetTransacoesAsync())
                .Where(t => t.Tipo == "Receita")
                .OrderByDescending(t => t.Valor)
                .ToList();

            var despesas = (await _db.GetTransacoesAsync())
                .Where(t => t.Tipo == "Despesa")
                .OrderByDescending(t => t.Valor)
                .ToList();

            PopularLista(_layoutReceitas, receitas);
            PopularLista(_layoutDespesas, despesas);
        }

        private void PopularLista(LinearLayout layout, System.Collections.Generic.List<Transacao> lista)
        {
            layout.RemoveAllViews();

            foreach (var item in lista)
            {
                var horizontalLayout = new LinearLayout(this)
                {
                    Orientation = Orientation.Horizontal
                };

                // Texto com nome e valor
                var textView = new TextView(this)
                {
                    Text = $"{item.Descricao} - R$ {item.Valor:F2}",
                    LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1)
                };

                // Botão Editar
                var btnEditar = new Button(this)
                {
                    Text = "Editar"
                };
                btnEditar.Click += (s, e) =>
                {
                    var intent = new Android.Content.Intent(this, typeof(CadastroActivity));
                    intent.PutExtra("Id", item.Id);
                    StartActivity(intent);
                };

                // Botão Excluir
                var btnExcluir = new Button(this)
                {
                    Text = "Excluir"
                };
                btnExcluir.Click += async (s, e) =>
                {
                    await _db.DeletarTransacaoAsync(item);
                    PopularLista(layout, lista.Where(t => t.Id != item.Id).ToList());
                };

                horizontalLayout.AddView(textView);
                horizontalLayout.AddView(btnEditar);
                horizontalLayout.AddView(btnExcluir);

                layout.AddView(horizontalLayout);
            }
        }
    }


}
