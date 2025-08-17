using Android.Content;
using Android.Views;
using PersonalFinance.Resources.Models;
using System.Globalization;

namespace PersonalFinance.Resources.Adapters
{
    public class DespesaAdapter : BaseAdapter<Despesa>
    {
        private readonly Context _context;
        private readonly List<Despesa> _despesas;

        public DespesaAdapter(Context context, List<Despesa> despesas)
        {
            _context = context;
            _despesas = despesas ?? new List<Despesa>();
        }

        public override Despesa this[int position] => _despesas[position];
        public override int Count => _despesas.Count;
        public override long GetItemId(int position) => position;

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var view = convertView ?? LayoutInflater.From(_context).Inflate(Resource.Layout.item_despesa, parent, false);

            var despesa = _despesas[position];

            view.FindViewById<TextView>(Resource.Id.tvDescricao).Text = despesa.Descricao;
            view.FindViewById<TextView>(Resource.Id.tvCategoria).Text = despesa.Categoria;
            view.FindViewById<TextView>(Resource.Id.tvValor).Text = $"R$ {despesa.Valor.ToString("N2", new CultureInfo("pt-BR"))}";

            var btnQuitar = view.FindViewById<Button>(Resource.Id.btnQuitar);
            btnQuitar.Click += (s, e) =>
            {
                QuitarDespesa(despesa);
            };

            return view;
        }

        private void QuitarDespesa(Despesa despesa)
        {
            // Exemplo simples: mostrar Toast e remover da lista
            Toast.MakeText(_context, $"Despesa '{despesa.Descricao}' quitada!", ToastLength.Short).Show();

            // Opcional: remover da lista e atualizar adapter
            _despesas.Remove(despesa);
            NotifyDataSetChanged();

            // Aqui você também pode atualizar no banco de dados:
            // using var db = new SQLiteConnection(dbPath);
            // despesa.Pago = true; // se tiver campo Pago
            // db.Update(despesa);
        }
    }
}
