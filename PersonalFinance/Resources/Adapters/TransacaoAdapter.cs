using Android.Content;
using Android.Views;
using Android.Widget;
using PersonalFinance.Resources.Models;
using System.Collections.Generic;
using System.Globalization;

namespace PersonalFinance.Resources.Adapters
{
    public class TransacaoAdapter : BaseAdapter<Transacao>
    {
        private readonly Context _context;
        private readonly List<Transacao> _transacoes;

        public TransacaoAdapter(Context context, List<Transacao> transacoes)
        {
            _context = context;
            _transacoes = transacoes ?? new List<Transacao>();
        }

        public override Transacao this[int position] => _transacoes[position];

        public override int Count => _transacoes.Count;

        public override long GetItemId(int position) => position;

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var view = convertView ?? LayoutInflater.From(_context)
                .Inflate(Resource.Layout.item_transacao, parent, false);

            var transacao = _transacoes[position];

            var tvDataValor = view.FindViewById<TextView>(Resource.Id.tvDataValor);
            var tvDespesa = view.FindViewById<TextView>(Resource.Id.tvDespesa);
            var tvReceita = view.FindViewById<TextView>(Resource.Id.tvReceita);
            var tvObservacao = view.FindViewById<TextView>(Resource.Id.tvObservacao);
            var tvValorDestaque = view.FindViewById<TextView>(Resource.Id.tvValorDestaque);

            // Descrição da despesa
            string despesaDesc = transacao.Despesa != null
                ? transacao.Despesa.Descricao
                : "Sem despesa";

            // Fonte pagadora da receita
            string receitaDesc = transacao.Despesa?.Receita != null
                ? transacao.Despesa.Receita.FontePagadora
                : "Sem receita";

            // Exibe Data + Valor
            tvDataValor.Text = $"{transacao.Data:dd/MM/yyyy} - R$ {transacao.Valor.ToString("N2", new CultureInfo("pt-BR"))}";

            // Exibe detalhes
            tvDespesa.Text = $"Despesa: {despesaDesc}";
            tvReceita.Text = $"Receita: {receitaDesc}";
            tvObservacao.Text = $"Observação: {transacao.Observacao ?? "Nenhuma"}";

            // Valor destacado → verde para crédito, vermelho para débito
            tvValorDestaque.Text = $"R$ {transacao.Valor.ToString("N2", new CultureInfo("pt-BR"))}";
            tvValorDestaque.SetTextColor(
                transacao.Valor >= 0
                    ? Android.Graphics.Color.ParseColor("#388E3C") // Verde
                    : Android.Graphics.Color.ParseColor("#D32F2F") // Vermelho
            );

            return view;
        }
    }
}
