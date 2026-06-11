using Android.Content;
using Android.Views;
using PersonalFinance.Resources.Activities;
using PersonalFinance.Resources.Models;
using System.Globalization;

namespace PersonalFinance.Resources.Adapters
{
    public class DespesaEmAbertoAdapter : BaseAdapter<DespesaEmAberto>
    {
        private static readonly CultureInfo PtBr = new("pt-BR");
        private readonly Context _context;
        private readonly List<DespesaEmAberto> _despesas;

        public DespesaEmAbertoAdapter(Context context, List<DespesaEmAberto> despesas)
        {
            _context = context;
            _despesas = despesas ?? new List<DespesaEmAberto>();
        }

        public override DespesaEmAberto this[int position] => _despesas[position];
        public override int Count => _despesas.Count;
        public override long GetItemId(int position) => _despesas[position].Despesa.Id;

        public override View GetView(int position, View? convertView, ViewGroup? parent)
        {
            var view = convertView ?? LayoutInflater.From(_context)
                .Inflate(Resource.Layout.item_despesa_em_aberto, parent, false);

            var item = _despesas[position];
            var despesa = item.Despesa;

            view.FindViewById<TextView>(Resource.Id.tvDescricaoAberta)!.Text = despesa.Descricao;
            view.FindViewById<TextView>(Resource.Id.tvCategoriaVencimento)!.Text =
                $"{despesa.Categoria} - Vence em {despesa.Vencimento:dd/MM/yyyy}";
            view.FindViewById<TextView>(Resource.Id.tvValorOriginal)!.Text =
                $"Valor: {FormatarMoeda(despesa.Valor)}";
            view.FindViewById<TextView>(Resource.Id.tvValorPago)!.Text =
                $"Pago: {FormatarMoeda(item.TotalPago)}";
            view.FindViewById<TextView>(Resource.Id.tvValorRestante)!.Text =
                $"Falta: {FormatarMoeda(item.ValorRestante)}";

            var tvSituacao = view.FindViewById<TextView>(Resource.Id.tvSituacaoAberta)!;
            if (item.TotalPago > 0)
            {
                tvSituacao.Text = "PARCIALMENTE QUITADA";
                tvSituacao.SetTextColor(Android.Graphics.Color.ParseColor("#B7791F"));
            }
            else if (despesa.Vencimento.Date < DateTime.Today)
            {
                tvSituacao.Text = "VENCIDA";
                tvSituacao.SetTextColor(Android.Graphics.Color.ParseColor("#C2414F"));
            }
            else
            {
                tvSituacao.Text = "PENDENTE";
                tvSituacao.SetTextColor(Android.Graphics.Color.ParseColor("#667785"));
            }

            var btnQuitar = view.FindViewById<Button>(Resource.Id.btnQuitarAberta)!;
            btnQuitar.Tag = new Java.Lang.Integer(despesa.Id);
            btnQuitar.Click -= AbrirQuitacao;
            btnQuitar.Click += AbrirQuitacao;

            return view;
        }

        private void AbrirQuitacao(object? sender, EventArgs e)
        {
            if (sender is not Button button ||
                !int.TryParse(button.Tag?.ToString(), out var despesaId))
            {
                return;
            }

            var item = _despesas.First(x => x.Despesa.Id == despesaId);
            var despesa = item.Despesa;
            var intent = new Intent(_context, typeof(TransacaoCreateActivity));
            intent.PutExtra("DespesaId", despesa.Id);
            intent.PutExtra("Descricao", despesa.Descricao);
            intent.PutExtra("Categoria", despesa.Categoria);
            intent.PutExtra("Valor", (double)item.ValorRestante);
            intent.PutExtra("Vencimento", despesa.Vencimento.Ticks);
            intent.PutExtra("ReceitaId", despesa.ReceitaId);

            _context.StartActivity(intent);
        }

        private static string FormatarMoeda(decimal valor) =>
            $"R$ {valor.ToString("N2", PtBr)}";
    }
}
