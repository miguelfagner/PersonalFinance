using Android.Content;
using Android.Views;
using PersonalFinance.Resources.Models;

namespace PersonalFinance.Resources.Adapters
{
    public class ReceitaAdapter : BaseAdapter<Receita>
    {
        private readonly Context _context;
        private readonly List<Receita> _receitas;

        public ReceitaAdapter(Context context, List<Receita> receitas)
        {
            _context = context;
            _receitas = receitas ?? new List<Receita>();
        }

        public override Receita this[int position] => _receitas[position];
        public override int Count => _receitas.Count;
        public override long GetItemId(int position) => position;

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var view = convertView ?? LayoutInflater.From(_context).Inflate(Resource.Layout.item_receita, parent, false);

            var receita = _receitas[position];

            // Mapeando os IDs corretos do item_receita.xml
            view.FindViewById<TextView>(Resource.Id.tvFontePagadora).Text = receita.FontePagadora;
            view.FindViewById<TextView>(Resource.Id.tvDescricaoReceita).Text = receita.Descricao;
            view.FindViewById<TextView>(Resource.Id.tvTipoReceita).Text = receita.Tipo;
            view.FindViewById<TextView>(Resource.Id.tvValorReceita).Text = $"R$ {receita.Valor:N2}";

            // Botão de editar (se você quiser implementar ação depois)
            var btnEditar = view.FindViewById<Button>(Resource.Id.btnEditarReceita);
            btnEditar.Click += (s, e) =>
            {
                Toast.MakeText(_context, $"Editar: {receita.Descricao}", ToastLength.Short).Show();
                // Aqui você pode abrir outra Activity para edição
            };

            return view;
        }
    }
}
