using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.ViewModels
{
    public class ListagemViewModel
    {
        private DatabaseService _db;

        public List<Transacao> Receitas { get; private set; }
        public List<Transacao> Despesas { get; private set; }
        public List<Transacao> Cofre { get; private set; }

        // Evento para avisar a View que os dados foram atualizados
        public event Action DadosAtualizados;

        public ListagemViewModel()
        {
            _db = new DatabaseService();
        }

        public async Task AtualizarListasAsync()
        {
            var transacoes = await _db.GetTransacoesAsync();

            Receitas = transacoes.ToList();
            Despesas = transacoes.ToList();
            Cofre = transacoes.ToList();

            DadosAtualizados?.Invoke();
        }

        public async Task DeletarTransacaoAsync(Transacao transacao)
        {
            await _db.DeletarTransacaoAsync(transacao);
            await AtualizarListasAsync();
        }
    }
}
