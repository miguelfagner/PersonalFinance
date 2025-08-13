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

        public Task<int> SalvarTransacaoAsync(Transacao transacao)
        {
            if (transacao.Id != 0)
                return _db.UpdateAsync(transacao);
            else
                return _db.InsertAsync(transacao);
        }

        public Task<List<Transacao>> ListarTransacoesAsync()
        {
            return _db.Table<Transacao>().ToListAsync();
        }

        public Task<int> DeletarTransacaoAsync(Transacao transacao)
        {
            return _db.DeleteAsync(transacao);
        }

        internal Task<List<Transacao>> GetTransacoesAsync()
        {
            return _db.Table<Transacao>().ToListAsync();
        }

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
            return _db.Table<Receita>().ToListAsync();
        }
    }
}
