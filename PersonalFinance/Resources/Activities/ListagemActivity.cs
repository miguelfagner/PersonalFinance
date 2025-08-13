using Android.Views;
using Android.Util;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Listagem")]
    public class ListagemActivity : Activity
    {
        DatabaseService _db;
        LinearLayout _layoutReceitas, _layoutDespesas, _layoutCofre;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_listagem);

            _db = new DatabaseService();

            _layoutReceitas = FindViewById<LinearLayout>(Resource.Id.layoutReceitas);
            _layoutCofre = FindViewById<LinearLayout>(Resource.Id.layoutCofre);
            _layoutDespesas = FindViewById<LinearLayout>(Resource.Id.layoutDespesas);

            var receitas = (await _db.GetTransacoesAsync())
                .Where(t => t.Tipo == "Receita")
                .OrderByDescending(t => t.Valor)
                .ToList();

            var despesas = (await _db.GetTransacoesAsync())
                .Where(t => t.Tipo == "Despesa")
                .OrderByDescending(t => t.Valor)
                .ToList();

            var cofre = (await _db.GetTransacoesAsync())
                .Where(t => t.Tipo == "Cofre")
                .OrderByDescending(t => t.Valor)
                .ToList();

            PopularLista(_layoutReceitas, receitas);
            PopularLista(_layoutCofre, cofre);
            PopularLista(_layoutDespesas, despesas);
        }

        protected override async void OnResume()
        {
            base.OnResume();

            _db = new DatabaseService();

            _layoutReceitas = FindViewById<LinearLayout>(Resource.Id.layoutReceitas);
            _layoutDespesas = FindViewById<LinearLayout>(Resource.Id.layoutDespesas);
            _layoutCofre = FindViewById<LinearLayout>(Resource.Id.layoutCofre);

            var receitas = (await _db.GetTransacoesAsync())
                .Where(t => t.Tipo == "Receita")
                .OrderByDescending(t => t.Valor)
                .ToList();

            var despesas = (await _db.GetTransacoesAsync())
                .Where(t => t.Tipo == "Despesa")
                .OrderByDescending(t => t.Valor)
                .ToList();

            var cofre = (await _db.GetTransacoesAsync())
                .Where(t => t.Tipo == "Cofre")
                .OrderByDescending(t => t.Valor)
                .ToList();

            PopularLista(_layoutReceitas, receitas);
            PopularLista(_layoutDespesas, despesas);
            PopularLista(_layoutCofre, cofre);
        }

        private void PopularLista(LinearLayout layout, System.Collections.Generic.List<Transacao> lista)
        {
            layout.RemoveAllViews();

            // Conversão de dp para px para manter proporção
            int larguraPx = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 60, Resources.DisplayMetrics);
            int alturaPx = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 35, Resources.DisplayMetrics);

            var layoutParamsBotao = new LinearLayout.LayoutParams(larguraPx, alturaPx);
            layoutParamsBotao.SetMargins(5, 0, 5, 0);

            foreach (var item in lista)
            {
                // Layout vertical: texto em cima, botões embaixo
                var verticalLayout = new LinearLayout(this)
                {
                    Orientation = Orientation.Vertical
                };

                // Texto da transação
                var textView = new TextView(this)
                {
                    Text = $"{item.Descricao} - R$ {item.Valor:F2}",
                    TextSize = 16
                };
                textView.SetTextColor(Android.Graphics.Color.Black);

                // Layout horizontal para os botões
                var botoesLayout = new LinearLayout(this)
                {
                    Orientation = Orientation.Horizontal
                };

                // Botão Editar (azul claro)
                var btnEditar = CriarBotao("Editar", layoutParamsBotao);
                btnEditar.SetBackgroundColor(Android.Graphics.Color.LightBlue);
                btnEditar.Click += (s, e) =>
                {
                    var intent = new Android.Content.Intent(this, typeof(CadastroActivity));
                    intent.PutExtra("Id", item.Id);
                    StartActivity(intent);
                };

                // Botão Excluir (vermelho)
                var btnExcluir = CriarBotao("Excluir", layoutParamsBotao);
                btnExcluir.SetBackgroundColor(Android.Graphics.Color.LightSalmon);
                btnExcluir.Click += async (s, e) =>
                {
                    await _db.DeletarTransacaoAsync(item);
                    PopularLista(layout, lista.Where(t => t.Id != item.Id).ToList());
                };

                // Botão Quitar (verde)
                var btnQuitar = CriarBotao("Quitar", layoutParamsBotao);
                btnQuitar.SetBackgroundColor(Android.Graphics.Color.LightGreen);
                btnQuitar.Click += async (s, e) =>
                {
                    // Exemplo: marcar como quitado
                    PopularLista(layout, lista.Where(t => t.Id != item.Id).ToList());
                };

                // Adiciona os botões no layout horizontal
                botoesLayout.AddView(btnEditar);
                botoesLayout.AddView(btnExcluir);
                botoesLayout.AddView(btnQuitar);

                // Adiciona o texto e os botões no layout vertical
                verticalLayout.AddView(textView);
                verticalLayout.AddView(botoesLayout);

                // Adiciona o bloco final na tela
                layout.AddView(verticalLayout);
            }
        }

        private Button CriarBotao(string texto, LinearLayout.LayoutParams parametros)
        {
            var btn = new Button(this)
            {
                Text = texto,
                TextSize = 10 // fonte pequena
            };
            btn.SetPadding(5, 0, 5, 0); // reduz o espaço interno
            btn.LayoutParameters = parametros;
            return btn;
        }
    }
}
