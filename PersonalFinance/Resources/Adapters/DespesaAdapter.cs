using Android.Content;
using Android.Views;
using Android.Widget;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

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
            view.FindViewById<TextView>(Resource.Id.tvValor).Text =
                $"R$ {despesa.Valor.ToString("N2", new CultureInfo("pt-BR"))}";

            var btnQuitar = view.FindViewById<Button>(Resource.Id.btnQuitar);

            // limpa handlers antigos
            btnQuitar.SetOnClickListener(null);

            // adiciona clique para quitar
            btnQuitar.Click += async (s, e) =>
            {
                await QuitarDespesaAsync(despesa);
            };

            return view;
        }

        private async Task QuitarDespesaAsync(Despesa despesa)
        {
            try
            {
                // Cria a transação no banco
                var transacao = new Transacao
                {
                    DespesaId = despesa.Id,
                    Valor = despesa.Valor,
                    Data = DateTime.Now,
                    Observacao = "Quitação automática"
                };

                await _db.SalvarTransacaoAsync(transacao);

                // Mostra mensagem
                Toast.MakeText(_context, $"Despesa '{despesa.Descricao}' quitada!", ToastLength.Short).Show();

                // Atualiza lista (se quiser remover da tela ao quitar, descomente)
                // _despesas.Remove(despesa);
                NotifyDataSetChanged();
            }
            catch (System.Exception ex)
            {
                Toast.MakeText(_context, $"Erro ao quitar: {ex.Message}", ToastLength.Long).Show();
            }
        }
    }
}
