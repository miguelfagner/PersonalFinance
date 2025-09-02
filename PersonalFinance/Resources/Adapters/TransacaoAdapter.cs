using Android.Content;
using Android.Views;
using PersonalFinance.Resources.Models;
using System.Globalization;

namespace PersonalFinance.Resources.Adapters
{
    public class TransacaoAdapter : BaseAdapter<TransacaoItem>
    {
        private readonly Context _context;
        private readonly List<TransacaoItem> _items;

        public TransacaoAdapter(Context context, List<Transacao> transacoes)
        {
            _context = context;

            // Organiza as transações por MÊS/ANO
            _items = new List<TransacaoItem>();

            var grupos = transacoes
                .OrderByDescending(t => t.Data) // mais recentes primeiro
                .GroupBy(t => new { t.Data.Year, t.Data.Month })
                .OrderByDescending(g => g.Key.Year)
                .ThenByDescending(g => g.Key.Month);

            foreach (var grupo in grupos)
            {
                string titulo = $"Transações {new DateTime(grupo.Key.Year, grupo.Key.Month, 1).ToString("MMMM/yy", new CultureInfo("pt-BR"))}";

                // adiciona cabeçalho
                _items.Add(new TransacaoItem
                {
                    IsHeader = true,
                    HeaderTitle = titulo
                });

                // adiciona transações do mês
                foreach (var t in grupo)
                {
                    _items.Add(new TransacaoItem
                    {
                        IsHeader = false,
                        Transacao = t
                    });
                }
            }
        }

        public override TransacaoItem this[int position] => _items[position];
        public override int Count => _items.Count;
        public override long GetItemId(int position) => position;

        public override int ViewTypeCount => 2;
        public override int GetItemViewType(int position) => _items[position].IsHeader ? 0 : 1;

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var item = _items[position];

            if (item.IsHeader)
            {
                // Cabeçalho
                var headerView = convertView ?? LayoutInflater.From(_context)
                    .Inflate(Resource.Layout.item_transacao_header, parent, false);

                var tvHeader = headerView.FindViewById<TextView>(Resource.Id.tvHeaderTransacao);
                tvHeader.Text = item.HeaderTitle;

                return headerView;
            }
            else
            {
                // Transação normal
                var view = convertView ?? LayoutInflater.From(_context)
                    .Inflate(Resource.Layout.item_transacao, parent, false);

                var transacao = item.Transacao;

                var tvDataValor = view.FindViewById<TextView>(Resource.Id.tvDataValor);
                var tvDespesa = view.FindViewById<TextView>(Resource.Id.tvDespesa);
                var tvReceita = view.FindViewById<TextView>(Resource.Id.tvReceita);
                var tvObservacao = view.FindViewById<TextView>(Resource.Id.tvObservacao);
                var tvValorDestaque = view.FindViewById<TextView>(Resource.Id.tvValorDestaque);

                string despesaDesc = transacao.Despesa != null
                    ? transacao.Despesa.Descricao
                    : "Sem despesa";

                string receitaDesc = transacao.Despesa?.Receita != null
                    ? transacao.Despesa.Receita.FontePagadora
                    : "Sem receita";

                tvDataValor.Text = $"{transacao.Data:dd/MM/yyyy} - R$ {transacao.Valor.ToString("N2", new CultureInfo("pt-BR"))}";
                tvDespesa.Text = $"Despesa: {despesaDesc}";
                tvReceita.Text = $"Receita: {receitaDesc}";
                tvObservacao.Text = $"Observação: {transacao.Observacao ?? "Nenhuma"}";

                tvValorDestaque.Text = $"R$ {transacao.Valor.ToString("N2", new CultureInfo("pt-BR"))}";
                tvValorDestaque.SetTextColor(
                    transacao.Valor >= 0
                        ? Android.Graphics.Color.ParseColor("#388E3C")
                        : Android.Graphics.Color.ParseColor("#D32F2F")
                );

                return view;
            }
        }
    }
}
