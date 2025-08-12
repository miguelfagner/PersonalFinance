using System;
using System.IO;
using System.Threading.Tasks;
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
    }
}
