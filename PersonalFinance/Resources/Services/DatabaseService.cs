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
        }

        //DESPESA
        internal Task<List<Despesa>> ListaDespesasAsync()
        {
            var ls = _db.Table<Despesa>().ToListAsync();

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

        public Task<int> DeletarDespesaAsync(Despesa despesa)
        {
            return _db.DeleteAsync(despesa);
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
