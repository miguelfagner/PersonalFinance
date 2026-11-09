using Android.Content;
using Android.Graphics;
using Android.Views;
using PersonalFinance.Resources.Models;
using System.Globalization;

namespace PersonalFinance.Resources.Adapters
{
    public class DespesaFixaAdapter : BaseAdapter<DespesaFixa>
    {
        private static readonly CultureInfo PtBr = new("pt-BR");
        private readonly Context _context;
        private readonly List<DespesaFixa> _itens;
        private readonly IReadOnlyDictionary<int, string> _fontesPorReceita;

        public DespesaFixaAdapter(
            Context context,
            IEnumerable<DespesaFixa>? itens,
            IReadOnlyDictionary<int, string>? fontesPorReceita = null)
        {
            _context = context;
            _itens = itens?.ToList() ?? new List<DespesaFixa>();
            _fontesPorReceita = fontesPorReceita ?? new Dictionary<int, string>();
        }

        public override DespesaFixa this[int position] => _itens[position];
        public override int Count => _itens.Count;
        public override long GetItemId(int position) => _itens[position].Id;

        public override View GetView(int position, View? convertView, ViewGroup? parent)
        {
            var view = convertView ?? LayoutInflater.From(_context)!
                .Inflate(Resource.Layout.item_despesa_fixa, parent, false)!;

            var item = _itens[position];
            view.FindViewById<TextView>(Resource.Id.tvDespesaFixaDescricao)!.Text = item.Descricao;
            view.FindViewById<TextView>(Resource.Id.tvDespesaFixaCategoria)!.Text = item.Categoria;
            view.FindViewById<TextView>(Resource.Id.tvDespesaFixaReceita)!.Text =
                _fontesPorReceita.TryGetValue(item.ReceitaFixaId, out string? fonte)
                    ? $"Receita: {fonte}"
                    : $"Receita fixa #{item.ReceitaFixaId}";
            view.FindViewById<TextView>(Resource.Id.tvDespesaFixaVencimento)!.Text =
                $"Vence no dia {item.DiaVencimento}";
            view.FindViewById<TextView>(Resource.Id.tvDespesaFixaValor)!.Text =
                $"R$ {item.Valor.ToString("N2", PtBr)}";

            var status = view.FindViewById<TextView>(Resource.Id.tvDespesaFixaStatus)!;
            status.Text = item.Ativa ? "ATIVA" : "PAUSADA";
            status.SetTextColor(Color.ParseColor(item.Ativa ? "#18794E" : "#B7791F"));
            view.Alpha = item.Ativa ? 1f : 0.72f;

            return view;
        }
    }
}
