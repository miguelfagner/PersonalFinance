using Android.App;
using Android.OS;
using Android.Widget;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Balanço Mensal")]
    public class BalancoDetailActivity : Activity
    {
        private TextView tvTotalReceita, tvTotalDespesa, tvSaldo, tvTotalQuitado, tvFaltaQuitar, tvResumo, tvGastosPessoais, tvGastosDomesticos;
        private ProgressBar progDespesa, progGastosDomesticos, progGastosPessoais;

        private DatabaseService _db;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_balanco);

            // TextViews
            tvTotalReceita = FindViewById<TextView>(Resource.Id.tvTotalReceita);
            tvTotalDespesa = FindViewById<TextView>(Resource.Id.tvTotalDespesa);
            tvTotalQuitado = FindViewById<TextView>(Resource.Id.tvTotalQuitado);
            tvGastosPessoais = FindViewById<TextView>(Resource.Id.tvGastosPessoais);
            tvGastosDomesticos = FindViewById<TextView>(Resource.Id.tvGastosDomesticos);
            tvSaldo = FindViewById<TextView>(Resource.Id.tvSaldo);
            tvFaltaQuitar = FindViewById<TextView>(Resource.Id.tvFaltaQuitar);
            tvResumo = FindViewById<TextView>(Resource.Id.tvResumo);

            // ProgressBars
            progDespesa = FindViewById<ProgressBar>(Resource.Id.progDespesa);
            progGastosPessoais = FindViewById<ProgressBar>(Resource.Id.progGastosPessoais);
            progGastosDomesticos = FindViewById<ProgressBar>(Resource.Id.progGastosDomesticos);

            _db = new DatabaseService();

            await CarregarDadosAsync();
        }

        private async Task CarregarDadosAsync()
        {
            var despesas = await _db.ListaDespesasAsync() ?? new List<Despesa>();
            var receitas = await _db.ListaReceitasAsync() ?? new List<Receita>();
            var transacoes = await _db.ListaTransacoesAsync() ?? new List<Transacao>();

            var hoje = DateTime.Today;
            var mes = hoje.Month;
            var ano = hoje.Year;

            decimal totalReceita = receitas
                .Where(r => r?.MesReferencia != null && r.MesReferencia.Month == mes && r.MesReferencia.Year == ano)
                .Sum(r => r?.Valor ?? 0);

            decimal totalDespesa = despesas
                .Where(d => d?.Vencimento != null && d.Vencimento.Month == mes && d.Vencimento.Year == ano)
                .Sum(d => d?.Valor ?? 0);

            decimal totalQuitado = transacoes
                .Where(r => r?.Data != null && r.Data.Month == mes && r.Data.Year == ano)
                .Sum(r => r?.Valor ?? 0);

            //total de gastos pessoais planejado
            decimal gastoPlanejado = despesas
                .Where(r => string.Equals(r?.Categoria, "PESSOAL", StringComparison.OrdinalIgnoreCase))
                .Sum(r => r?.Valor ?? 0);

            //total de gastos pessoais gasto
            decimal gastoPessoal = transacoes
                .Where(r => string.Equals(r?.Despesa?.Categoria, "PESSOAL", StringComparison.OrdinalIgnoreCase))
                .Sum(r => r?.Valor ?? 0);

            //total de gastos domésticos planejado
            decimal gastoDomesticoPlanejado = despesas
                .Where(r => string.Equals(r?.Categoria, "GASTOS DOMESTICOS", StringComparison.OrdinalIgnoreCase))
                .Sum(r => r?.Valor ?? 0);

            //total de gastos domésticos gasto
            decimal gastoDomestico = transacoes
                .Where(r => string.Equals(r?.Despesa?.Categoria, "GASTOS DOMESTICOS", StringComparison.OrdinalIgnoreCase))
                .Sum(r => r?.Valor ?? 0);


            var saldo = totalReceita - totalDespesa;
            var faltaQuitar = totalDespesa - totalQuitado;

            // Atualiza textos
            tvTotalReceita.Text = $"RECEITA: R${totalReceita:N2}";
            tvTotalDespesa.Text = $"DESPESAS: R${totalDespesa:N2}";
            tvTotalQuitado.Text = $"DESPESAS QUITADAS: R${totalQuitado:N2}";
            tvFaltaQuitar.Text = $"AGUARDANDO QUITAR: R${faltaQuitar:N2}";
            tvGastosPessoais.Text = $"GASTOS PESSOAIS R${gastoPlanejado:N2}";
            tvGastosDomesticos.Text = $"GASTOS DOMESTICOS R${gastoDomesticoPlanejado:N2}";
            tvSaldo.Text = $"POUPADO: R$ {saldo:N2}";

            // Atualiza indicadores
            progDespesa.Progress = totalDespesa > 0 ? (int)((totalQuitado / totalDespesa) * 100) : 0;
            progGastosPessoais.Progress = gastoPlanejado > 0 ? (int)((gastoPessoal / gastoPlanejado) * 100) : 0;
            progGastosDomesticos.Progress = gastoDomesticoPlanejado > 0 ? (int)((gastoDomestico / gastoDomesticoPlanejado) * 100) : 0;


            // Resumo de desempenho
            if (saldo > 0)
            {
                tvResumo.Text = "Você está no positivo! 👏";
                tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#4CAF50"));
            }
            else if (saldo == 0)
            {
                tvResumo.Text = "Suas contas estão equilibradas.";
                tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#FF9800"));
            }
            else
            {
                tvResumo.Text = "Atenção! Saldo negativo.";
                tvResumo.SetTextColor(Android.Graphics.Color.ParseColor("#F44336"));
            }
        }
    }
}
