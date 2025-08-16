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

        //public Task<int> DeletarTransacaoAsync(Transacao transacao)
        //{
        //    return _db.DeleteAsync(transacao);
        //}

        internal Task<int> SalvarReceitaAsync(Receita receita)
        {
            if (receita.Id != 0)
                return _db.UpdateAsync(receita);
            else
                return _db.InsertAsync(receita);
        }

        internal Task<int> SalvarDespesaAsync(Despesa despesa)
        {
            if (despesa.Id != 0)
                return _db.UpdateAsync(despesa);
            else
                return _db.InsertAsync(despesa);
        }

        internal Task<List<Receita>> ListaReceitasAsync()
        {
            var ls = _db.Table<Receita>().OrderByDescending(r => r.MesReferencia).ToListAsync();

            return ls;
        }

        internal Task<List<Despesa>> ListaDespesasAsync()
        {
            var ls = _db.Table<Despesa>().ToListAsync();

            return ls;
        }

        internal Task<Despesa> PegarDespesaAsync(int despesaId)
        {
            return _db.FindAsync<Despesa>(despesaId);
        }

        internal Task<Despesa> PegarReceitaAsync(int receitaId)
        {
            return _db.FindAsync<Despesa>(receitaId);
        }
    }
}
