using Android.Content;
using Android.Graphics;
using Android.Views;
using PersonalFinance.Resources.Models;
using System.Globalization;

namespace PersonalFinance.Resources.Adapters
{
    public class ReceitaFixaAdapter : BaseAdapter<ReceitaFixa>
    {
        private static readonly CultureInfo PtBr = new("pt-BR");
        private readonly Context _context;
        private readonly List<ReceitaFixa> _itens;

        public ReceitaFixaAdapter(Context context, IEnumerable<ReceitaFixa>? itens)
        {
            _context = context;
            _itens = itens?.ToList() ?? new List<ReceitaFixa>();
        }

        public override ReceitaFixa this[int position] => _itens[position];
        public override int Count => _itens.Count;
        public override long GetItemId(int position) => _itens[position].Id;

        public override View GetView(int position, View? convertView, ViewGroup? parent)
        {
            var view = convertView ?? LayoutInflater.From(_context)!
                .Inflate(Resource.Layout.item_receita_fixa, parent, false)!;

            var item = _itens[position];
            view.FindViewById<TextView>(Resource.Id.tvReceitaFixaFonte)!.Text = item.FontePagadora;
            view.FindViewById<TextView>(Resource.Id.tvReceitaFixaDescricao)!.Text = item.Descricao;
            view.FindViewById<TextView>(Resource.Id.tvReceitaFixaTipo)!.Text = item.Tipo;
            view.FindViewById<TextView>(Resource.Id.tvReceitaFixaValor)!.Text =
                $"R$ {item.Valor.ToString("N2", PtBr)}";

            var status = view.FindViewById<TextView>(Resource.Id.tvReceitaFixaStatus)!;
            status.Text = item.Ativa ? "ATIVA" : "PAUSADA";
            status.SetTextColor(Color.ParseColor(item.Ativa ? "#18794E" : "#B7791F"));
            view.Alpha = item.Ativa ? 1f : 0.72f;

            return view;
        }
    }
}
