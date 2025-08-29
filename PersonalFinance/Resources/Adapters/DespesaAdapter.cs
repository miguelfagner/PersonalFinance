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
            var view = convertView ?? LayoutInflater.From(_context)
                .Inflate(Resource.Layout.item_despesa, parent, false);
            var despesa = _despesas[position];

            // Atualiza textos
            view.FindViewById<TextView>(Resource.Id.tvDescricao).Text = despesa.Descricao;
            view.FindViewById<TextView>(Resource.Id.tvCategoria).Text = despesa.Categoria;
            view.FindViewById<TextView>(Resource.Id.tvValor).Text =
                $"R$ {despesa.Valor.ToString("N2", new CultureInfo("pt-BR"))}";

            var btnQuitar = view.FindViewById<Button>(Resource.Id.btnQuitar);

            // Define visibilidade do botão
            btnQuitar.Visibility = despesa.Sttatus == true ? ViewStates.Gone : ViewStates.Visible;

            // Define cor do botão conforme status
            if (despesa.Sttatus == null) // pendente
            {
                btnQuitar.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(
                    Android.Graphics.Color.ParseColor("#198754")); // azul
            }
            else if (despesa.Sttatus == false) // parcialmente quitado
            {
                btnQuitar.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(
                    Android.Graphics.Color.ParseColor("#ffc107")); // amarelo
            }

            // Limpa handlers antigos
            btnQuitar.SetOnClickListener(null);

            // Adiciona clique para quitar apenas se estiver visível
            if (btnQuitar.Visibility == ViewStates.Visible)
            {
                btnQuitar.Click += async (s, e) =>
                {
                    await QuitarDespesaAsync(despesa);
                    // Atualiza visual do botão sem recarregar toda a lista
                    btnQuitar.Visibility = ViewStates.Gone;
                };
            }

            return view;
        }



        private async Task QuitarDespesaAsync(Despesa despesa)
        {
            try
            {
                var transacoes = await _db.ListaTransacoesAsync(despesa.Id);
                var pago = transacoes.Sum(x => x.Valor);
                var valor = despesa.Valor;

                if (pago > 0)
                    valor = despesa.Valor - pago;

                // Cria a transação no banco
                var transacao = new Transacao
                {
                    DespesaId = despesa.Id,
                    Valor = valor,
                    Data = DateTime.Now,
                    Observacao = "Quitação automática"
                };

                await _db.SalvarTransacaoAsync(transacao);

                // Atualiza o status da despesa no banco
                await _db.AtualizaStatusAsync(despesa.Id);

                // Atualiza o status local para true (quitado)
                despesa.Sttatus = true;

                // Atualiza a lista, assim o GetView será chamado e o botão sumirá
                NotifyDataSetChanged();

                Toast.MakeText(_context, $"Despesa '{despesa.Descricao}' quitada!", ToastLength.Short).Show();
            }
            catch (System.Exception ex)
            {
                Toast.MakeText(_context, $"Erro ao quitar: {ex.Message}", ToastLength.Long).Show();
            }
        }

    }
}
