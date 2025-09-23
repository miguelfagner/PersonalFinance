using Android.Content;
using PersonalFinance.Resources.Models;
using SQLite;

namespace PersonalFinance.Resources.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _db;

        public DatabaseService()
        {
            string dbPath = Path.Combine(Android.App.Application.Context.FilesDir.AbsolutePath, "financeiro.db");

            _db = new SQLiteAsyncConnection(dbPath);
           
            _db.CreateTableAsync<Receita>().Wait();
            _db.CreateTableAsync<Despesa>().Wait();
            _db.CreateTableAsync<Banco>().Wait();
            _db.CreateTableAsync<Transacao>().Wait();

    //        // limpa transações órfãs
    //        _db.ExecuteAsync(@"
    //    DELETE FROM Transacao
    //    WHERE DespesaId NOT IN (SELECT Id FROM Despesa);
    //").Wait();
        }

        //DESPESA
        internal Task<List<Despesa>> ListaDespesasAsync()
        {
            var ls = _db.Table<Despesa>().OrderByDescending(x=>x.Valor).ToListAsync();

            return ls;
        }
        internal Task<int> SalvarDespesaAsync(Despesa despesa)
        {
            if (despesa.Id != 0)
                return _db.UpdateAsync(despesa);
            else
                return _db.InsertAsync(despesa);
        }

        internal Task<Despesa> PegarDespesaAsync(int despesaId)
        {
            return _db.FindAsync<Despesa>(despesaId);
        }

        public async Task<int> DeletarDespesaAsync(Despesa despesa)
        {
            // Pega todas as transações da despesa
            var transacoes = await ListaTransacoesAsync();
            var transacoesDaDespesa = transacoes
                .Where(x => x.DespesaId == despesa.Id)
                .ToList(); // converte para lista

            // Deleta todas as transações relacionadas
            if (transacoesDaDespesa.Any())
            {
                foreach (var transacao in transacoesDaDespesa)
                {
                    await _db.DeleteAsync(transacao);
                }
            }

            // Deleta a despesa
            return await _db.DeleteAsync(despesa);
        }

        public async Task<int> AtualizaStatusAsync(int IdDespesa)
        {
            var despesa = await _db.FindAsync<Despesa>(IdDespesa);
            if (despesa == null)
                throw new ArgumentException("Despesa not found");

            var transacoes = await _db.Table<Transacao>()
                                       .Where(t => t.DespesaId == IdDespesa)
                                       .ToListAsync();

            var totalPago = transacoes.Sum(x => x.Valor);

            if (totalPago == despesa.Valor)
            {
                despesa.Sttatus = true; // quitado
            }
            else if (totalPago > 0 && totalPago < despesa.Valor)
            {
                despesa.Sttatus = false; // parcialmente quitado
            }
            else
            {
                despesa.Sttatus = null; // pendente
            }

            return await _db.UpdateAsync(despesa);
        }

        //RECEITA
        internal Task<List<Receita>> ListaReceitasAsync()
        {
            var ls = _db.Table<Receita>().OrderByDescending(r => r.MesReferencia).ToListAsync();

            return ls;
        }

        internal Task<int> SalvarReceitaAsync(Receita receita)
        {
            if (receita.Id != 0)
                return _db.UpdateAsync(receita);
            else
                return _db.InsertAsync(receita);
        }

        internal Task<Receita> PegarReceitaAsync(int receitaId)
        {
            return _db.FindAsync<Receita>(receitaId);
        }

        public Task<int> DeletarReceitaAsync(Receita receita)
        {
            return _db.DeleteAsync(receita);
        }

        //TRANSACOES
        internal async Task<List<Transacao>> ListaTransacoesAsync()
        {
            // Busca todas as transações
            var transacoes = await _db.Table<Transacao>()
                                      .OrderByDescending(t => t.Data)
                                      .ToListAsync();

            foreach (var t in transacoes)
            {
                // Busca a despesa
                t.Despesa = await _db.FindAsync<Despesa>(t.DespesaId);

                if (t.Despesa != null)
                {
                    // Busca a receita vinculada à despesa
                    t.Despesa.Receita = await _db.FindAsync<Receita>(t.Despesa.ReceitaId);
                }
            }

            return transacoes;
        }

        internal async Task<List<Transacao>> ListaTransacoesAsync(int id)
        {
            // Busca todas as transações
            var transacoes = await _db.Table<Transacao>()
                                      .Where(x=>x.DespesaId == id)
                                      .OrderByDescending(t => t.Data)
                                      .ToListAsync();

            foreach (var t in transacoes)
            {
                // Busca a despesa
                t.Despesa = await _db.FindAsync<Despesa>(t.DespesaId);

                if (t.Despesa != null)
                {
                    // Busca a receita vinculada à despesa
                    t.Despesa.Receita = await _db.FindAsync<Receita>(t.Despesa.ReceitaId);
                }
            }

            return transacoes;
        }

        internal Task<int> SalvarTransacaoAsync(Transacao transacao)
        {
            if (transacao.Id != 0)
                return _db.UpdateAsync(transacao);
            else
                return _db.InsertAsync(transacao);
        }

        internal Task<Transacao> PegarTransacaoAsync(int transacaoId)
        {
            return _db.FindAsync<Transacao>(transacaoId);
        }

        public Task<int> DeletarTransacaoAsync(Transacao transacao)
        {
            return _db.DeleteAsync(transacao);
        }
    }
}
