using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.Linq;

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
            _layoutDespesas = FindViewById<LinearLayout>(Resource.Id.layoutDespesas);
            _layoutCofre = FindViewById<LinearLayout>(Resource.Id.layoutCofre);

            await AtualizarListasAsync();
        }

        protected override async void OnResume()
        {
            base.OnResume();
            await AtualizarListasAsync();
        }

        private async System.Threading.Tasks.Task AtualizarListasAsync()
        {
            var transacoes = await _db.GetTransacoesAsync();

            PopularLista(_layoutReceitas, transacoes.Where(t => t.Tipo == "Receita").ToList());
            PopularLista(_layoutDespesas, transacoes.Where(t => t.Tipo == "Despesa").ToList());
            PopularLista(_layoutCofre, transacoes.Where(t => t.Tipo == "Cofre").ToList());
        }
        private void PopularLista(LinearLayout container, System.Collections.Generic.List<Transacao> lista)
        {
            container.RemoveAllViews();

            foreach (var item in lista)
            {
                // Container horizontal para texto + botões
                var itemLayout = new LinearLayout(this)
                {
                    Orientation = Orientation.Horizontal,
                    LayoutParameters = new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MatchParent,
                        ViewGroup.LayoutParams.WrapContent)
                };

                // Texto da transação (peso 1 para ocupar o máximo possível)
                var tv = new TextView(this)
                {
                    Text = $"{item.Descricao.ToUpper()} - R$ {item.Valor:F2}",
                    TextSize = 16,
                    LayoutParameters = new LinearLayout.LayoutParams(
                        0,
                        ViewGroup.LayoutParams.WrapContent, 1f)
                };
                tv.SetTextColor(Color.Black);
                tv.SetPadding(10, 10, 10, 10);

                // Layout horizontal para botões (para colocar margem vertical)
                var botoesLayout = new LinearLayout(this)
                {
                    Orientation = Orientation.Horizontal,
                    LayoutParameters = new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.WrapContent,
                        ViewGroup.LayoutParams.WrapContent)
                    {
                        TopMargin = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 2, Resources.DisplayMetrics),
                        BottomMargin = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 2, Resources.DisplayMetrics)
                    }
                };

                int larguraPx = (int)TypedValue.ApplyDimension(
                    ComplexUnitType.Dip, 70, Resources.DisplayMetrics);
                int alturaPx = (int)TypedValue.ApplyDimension(
                    ComplexUnitType.Dip, 36, Resources.DisplayMetrics);

                // Margem horizontal para cada botão
                var btnParams = new LinearLayout.LayoutParams(larguraPx, alturaPx) { LeftMargin = 8 };

                var btnEditar = new Button(this)
                {
                    Text = "Editar",
                    LayoutParameters = btnParams,
                    TextSize = 12,
                };
                btnEditar.SetBackgroundColor(Color.ParseColor("#4CAF50"));
                btnEditar.SetTextColor(Color.White);
                btnEditar.Click += (s, e) =>
                {
                    var intent = new Android.Content.Intent(this, typeof(CadastroActivity));
                    intent.PutExtra("Id", item.Id);
                    StartActivity(intent);
                };

                //var btnExcluir = new Button(this)
                //{
                //    Text = "Excluir",
                //    LayoutParameters = btnParams,
                //    TextSize = 12,
                //};
                //btnExcluir.SetBackgroundColor(Color.ParseColor("#F44336"));
                //btnExcluir.SetTextColor(Color.White);
                //btnExcluir.Click += async (s, e) =>
                //{
                //    await _db.DeletarTransacaoAsync(item);
                //    await AtualizarListasAsync();
                //};

                if (item.Tipo == "Despesa")
                {
                    var btnQuitar = new Button(this)
                    {
                        Text = "Quitar",
                        LayoutParameters = btnParams,
                        TextSize = 12,
                    };
                    btnQuitar.SetBackgroundColor(Color.ParseColor("#2196F3"));
                    btnQuitar.SetTextColor(Color.White);
                    btnQuitar.Click += async (s, e) =>
                    {
                        // Sua lógica para quitar
                        await AtualizarListasAsync();
                    };
                    botoesLayout.AddView(btnQuitar);
                }


                // Adiciona botões no layout horizontal
                botoesLayout.AddView(btnEditar);
                //botoesLayout.AddView(btnExcluir);

                // Adiciona texto e botões no item principal
                itemLayout.AddView(tv);
                itemLayout.AddView(botoesLayout);

                container.AddView(itemLayout);
            }
        }


    }
}
