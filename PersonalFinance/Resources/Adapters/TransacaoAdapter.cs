using Android.Views;
using PersonalFinance.Resources.Models;

namespace PersonalFinance.Resources.Adapters
{
    public class TransacaoAdapter : BaseAdapter<Transacao>
    {
        private readonly Activity _context;
        private readonly List<Transacao> _items;
        private readonly Action<Transacao> _onEditar;
        private readonly Action<Transacao> _onExcluir;

        public TransacaoAdapter(Activity context, List<Transacao> items, Action<Transacao> onEditar, Action<Transacao> onExcluir)
        {
            _context = context;
            _items = items;
            _onEditar = onEditar;
            _onExcluir = onExcluir;
        }

        public override Transacao this[int position] => _items[position];

        public override int Count => _items.Count;

        public override long GetItemId(int position) => position;

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var item = _items[position];
            var view = convertView ?? _context.LayoutInflater.Inflate(Resource.Layout.item_transacao, null);

            view.FindViewById<TextView>(Resource.Id.txtDescricao).Text = item.Descricao;
            view.FindViewById<TextView>(Resource.Id.txtValor).Text = item.Valor.ToString("C");

            view.FindViewById<Button>(Resource.Id.btnEditar).Click += (s, e) => _onEditar?.Invoke(item);
            view.FindViewById<Button>(Resource.Id.btnExcluir).Click += (s, e) => _onExcluir?.Invoke(item);

            return view;
        }
    }
}
