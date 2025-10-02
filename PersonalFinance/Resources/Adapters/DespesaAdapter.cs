using Android.Content;
using Android.Views;
using PersonalFinance.Resources.Activities;
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
                btnQuitar.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(
                    Android.Graphics.Color.ParseColor("#198754")); // verde
            else if (despesa.Sttatus == false) // parcialmente quitado
                btnQuitar.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(
                    Android.Graphics.Color.ParseColor("#ffc107")); // amarelo

            // Remove handlers antigos para evitar múltiplos eventos
            btnQuitar.Click -= async (s, e) => { };

            // Ao clicar, abre a TransacaoCreateActivity preenchida com os dados da despesa
            if (btnQuitar.Visibility == ViewStates.Visible)
            {
                btnQuitar.Click += (s, e) =>
                {
                    var intent = new Intent(_context, typeof(TransacaoCreateActivity));
                    intent.PutExtra("DespesaId", despesa.Id);
                    intent.PutExtra("Descricao", despesa.Descricao);
                    intent.PutExtra("Categoria", despesa.Categoria);
                    intent.PutExtra("Valor", (double)despesa.Valor);
                    intent.PutExtra("Vencimento", despesa.Vencimento.Ticks);
                    intent.PutExtra("ReceitaId", despesa.ReceitaId);

                    _context.StartActivity(intent);
                };
            }

            return view;
        }
    }
}
