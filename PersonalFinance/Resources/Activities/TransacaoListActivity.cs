using Android.Content;
using PersonalFinance.Resources.Adapters;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Transações")]
    public class TransacaoListActivity : Activity
    {
        private ListView _listView;
        private DatabaseService _db;
        private List<Transacao> _transacoes;
        private TransacaoAdapter _adapter;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.activity_transacao_list);

            _db = new DatabaseService();

            // Vincular componentes
            _listView = FindViewById<ListView>(Resource.Id.listViewTransacoes);

            await CarregarTransacoes();

            // Clique em item → editar (ignora cabeçalhos)
            _listView.ItemClick += (s, e) =>
            {
                var item = _adapter[e.Position];

                if (!item.IsHeader) // só abre se for transação
                {
                    var transacao = item.Transacao;
                    var intent = new Intent(this, typeof(TransacaoEditActivity));
                    intent.PutExtra("TransacaoId", transacao.Id);
                    StartActivity(intent);
                }
            };
        }

        protected override async void OnResume()
        {
            base.OnResume();
            await CarregarTransacoes();
        }

        private async Task CarregarTransacoes()
        {
            // Carrega todas as transações
            _transacoes = await _db.ListaTransacoesAsync();

            // Verifica se veio uma categoria (PieChart)
            string? categoria = Intent.GetStringExtra("Categoria");

            if (!string.IsNullOrEmpty(categoria))
            {
                // Filtra transações pela categoria da despesa
                _transacoes = _transacoes
                    .Where(t => t.Despesa != null &&
                                string.Equals(t.Despesa.Categoria, categoria, StringComparison.OrdinalIgnoreCase) &&
                                t.Despesa.Categoria == categoria)
                    .ToList();

                // Define título amigável
                Title = $"Transações - {categoria}";
            }

            // Verifica se veio uma semana (BarChart)
            int mes = Intent.GetIntExtra("Mes", 0);
            int ano = Intent.GetIntExtra("Ano", 0);
            int semana = Intent.GetIntExtra("Semana", 0);

            if (mes > 0 && ano > 0 && semana > 0)
            {
                int GetWeekOfMonth(DateTime date)
                {
                    var firstDay = new DateTime(date.Year, date.Month, 1);
                    int firstWeekOffset = (int)firstDay.DayOfWeek;
                    return ((date.Day + firstWeekOffset - 1) / 7) + 1;
                }

                _transacoes = _transacoes
                    .Where(t => t.Despesa.Categoria.Equals("PESSOAL") &&
                                t.Data.Month == mes &&
                                t.Data.Year == ano &&
                                GetWeekOfMonth(t.Data) == semana)
                    .ToList();

                // Define título amigável
                var nomeMes = new DateTime(ano, mes, 1).ToString("MMMM"); // exemplo: "setembro"
                Title = $"Semana {semana} - {nomeMes}/{ano}";
            }

            _adapter = new TransacaoAdapter(this, _transacoes);
            _listView.Adapter = _adapter;
        }



        //private async Task CarregarTransacoes()
        //{
        //    //// ListaTransacoesAsync já carrega Despesa e Receita
        //    //_transacoes = await _db.ListaTransacoesAsync();

        //    //_adapter = new TransacaoAdapter(this, _transacoes);
        //    //_listView.Adapter = _adapter;

        //    // Carrega todas as transações
        //    _transacoes = await _db.ListaTransacoesAsync();

        //    // Verifica se veio uma categoria do Intent
        //    string? categoria = Intent.GetStringExtra("Categoria");

        //    if (!string.IsNullOrEmpty(categoria))
        //    {
        //        // Filtra transações pela categoria da despesa
        //        _transacoes = _transacoes
        //            .Where(t => t.Despesa != null &&
        //                        string.Equals(t.Despesa.Categoria, categoria, StringComparison.OrdinalIgnoreCase))
        //            .ToList();
        //    }

        //    _adapter = new TransacaoAdapter(this, _transacoes);
        //    _listView.Adapter = _adapter;
        //}


    }
}
