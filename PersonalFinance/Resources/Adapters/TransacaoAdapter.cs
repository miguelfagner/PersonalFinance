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
                    HeaderTitle = titulo,
                    HeaderTotal = transacoes.Sum(x=>x.Valor)
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

                var tvHeader = headerView.FindViewById<TextView>(Resource.Id.tvHeaderMesTransacao);
                tvHeader.Text = item.HeaderTitle;    

                var tvMesTransacao = headerView.FindViewById<TextView>(Resource.Id.tvHeaderTransacao);
                tvMesTransacao.Text = $"TOTAL: R$ {item.HeaderTotal.ToString("N2", new CultureInfo("pt-BR"))}";

                return headerView;
            }
            else
            {
                // Transação normal
                var view = convertView ?? LayoutInflater.From(_context)
                    .Inflate(Resource.Layout.item_transacao, parent, false);

                var transacao = item.Transacao;

                var tvDataCategoria = view.FindViewById<TextView>(Resource.Id.tvDataCategoria);
                var tvDespesa = view.FindViewById<TextView>(Resource.Id.tvDespesa);
                var tvValorDestaque = view.FindViewById<TextView>(Resource.Id.tvValorDestaque);

                var textoDespesa = transacao.Observacao == "Quitação automática" ? transacao.Despesa.Descricao?.ToUpper() : transacao.Observacao.ToUpper();

                tvDataCategoria.Text = $"{textoDespesa }";
                tvDespesa.Text = $"DESPESA DE {transacao.Despesa.Descricao?.ToUpper() + " PAGA EM " + transacao.Data.ToString("dd/MM/yy")}";

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
