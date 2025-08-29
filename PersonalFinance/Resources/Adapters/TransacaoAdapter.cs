using Android.Content;
using Android.Views;
using Android.Widget;
using PersonalFinance.Resources.Models;
using System.Globalization;
using System.Collections.Generic;

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

            var textInfo = view.FindViewById<TextView>(Resource.Id.textTransacaoInfo);
            var textDetalhe = view.FindViewById<TextView>(Resource.Id.textTransacaoDetalhe);

            // Descrição da despesa
            string despesaDesc = transacao.Despesa != null
                ? transacao.Despesa.Descricao
                : "Sem despesa";

            // Fonte pagadora da receita
            string receitaDesc = transacao.Despesa?.Receita != null
                ? transacao.Despesa.Receita.FontePagadora
                : "Sem receita";

            textInfo.Text = $"{transacao.Data:dd/MM/yyyy} - R$ {transacao.Valor.ToString("N2", new CultureInfo("pt-BR"))}";
            textDetalhe.Text = $"Despesa: {despesaDesc} | Receita: {receitaDesc}\nObservação: {transacao.Observacao}";

            return view;
        }
    }
}
