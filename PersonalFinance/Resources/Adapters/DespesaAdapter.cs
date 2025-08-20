using Android.Content;
using Android.Views;
using Android.Widget;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.Globalization;

namespace PersonalFinance.Resources.Adapters
{
    public class DespesaAdapter : BaseAdapter<Despesa>
    {
        private readonly Context _context;
        private readonly List<Despesa> _despesas;
        private readonly DatabaseService _db;

        public DespesaAdapter(Context context, List<Despesa> despesas)
        {
            _context = context;
            _despesas = despesas ?? new List<Despesa>();
            _db = new DatabaseService();
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

            // remove handlers antigos para evitar múltiplos cliques acumulados
            btnQuitar.Click -= (s, e) => { };
            btnQuitar.Click += async (s, e) =>
            {
                await QuitarDespesaAsync(despesa);
            };

            return view;
        }

        private async Task QuitarDespesaAsync(Despesa despesa)
        {
            // Mostra mensagem
            Toast.MakeText(_context, $"Despesa '{despesa.Descricao}' quitada!", ToastLength.Short).Show();

            // Remove a despesa da lista e atualiza a tela
            //_despesas.Remove(despesa);
            NotifyDataSetChanged();

            // Cria a transação no banco
            var transacao = new Transacao
            {
                DespesaId = despesa.Id,
                Valor = despesa.Valor,
                Data = DateTime.Now
            };

            await _db.SalvarTransacoesAsync(transacao);

            // Opcional: atualizar o status da própria despesa
            // despesa.Paga = true;
            // await _db.UpdateDespesaAsync(despesa);
        }
    }
}
