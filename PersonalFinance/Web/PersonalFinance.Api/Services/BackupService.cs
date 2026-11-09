using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Data;

namespace PersonalFinance.Api.Services;

public sealed class BackupService(FinanceDbContext db)
{
    public async Task<byte[]> ExportAsync(CancellationToken ct)
    {
        var path = Path.Combine(Path.GetTempPath(), $"finance-backup-{Guid.NewGuid():N}.db");
        try
        {
            await using var read = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            await using (var sqlite = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                await sqlite.OpenAsync(ct);
                await using var write = await sqlite.BeginTransactionAsync(ct);
                async Task Table(string name, string columns)
                {
                    using var command = sqlite.CreateCommand(); command.Transaction = (SqliteTransaction)write;
                    command.CommandText = $"CREATE TABLE {name} (Id INTEGER PRIMARY KEY, ClientId TEXT, {columns})"; await command.ExecuteNonQueryAsync(ct);
                }
                async Task Row(string table, params object?[] values)
                {
                    using var command = sqlite.CreateCommand(); command.Transaction = (SqliteTransaction)write;
                    command.CommandText = $"INSERT INTO {table} VALUES ({string.Join(',', Enumerable.Range(0, values.Length).Select(x => "@p" + x))})";
                    for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue("@p" + i, values[i] ?? DBNull.Value);
                    await command.ExecuteNonQueryAsync(ct);
                }
                await Table("Receita", "MesReferencia INTEGER, FontePagadora TEXT, Descricao TEXT, Tipo TEXT, Valor TEXT, ReceitaFixaId INTEGER, CompetenciaGeracao INTEGER");
                await Table("Despesa", "ReceitaId INTEGER, DataCadastro INTEGER, Vencimento INTEGER, NParcela INTEGER, Descricao TEXT, Categoria TEXT, Valor TEXT, Sttatus INTEGER, replicavel INTEGER, DespesaFixaId INTEGER, CompetenciaGeracao INTEGER");
                await Table("Transacao", "DespesaId INTEGER, Observacao TEXT, Valor TEXT, Data INTEGER");
                await Table("ReceitaFixa", "FontePagadora TEXT, Descricao TEXT, Tipo TEXT, Valor TEXT, Ativa INTEGER");
                await Table("DespesaFixa", "ReceitaFixaId INTEGER, Descricao TEXT, Categoria TEXT, Valor TEXT, DiaVencimento INTEGER, Ativa INTEGER");
                await Table("PreparacaoMensalItem", "TipoModelo TEXT, ModeloId INTEGER, Competencia INTEGER, Estado TEXT, AtualizadoEm INTEGER");
                using (var command = sqlite.CreateCommand()) { command.Transaction = (SqliteTransaction)write; command.CommandText = "CREATE TABLE Banco (Id INTEGER PRIMARY KEY, Nome TEXT)"; await command.ExecuteNonQueryAsync(ct); }
                foreach (var x in await db.FixedIncomes.AsNoTracking().ToListAsync(ct)) await Row("ReceitaFixa", x.Id, x.ClientId.ToString(), x.Payer, x.Description, x.Type, Money(x.Amount), x.Active);
                foreach (var x in await db.FixedExpenses.AsNoTracking().ToListAsync(ct)) await Row("DespesaFixa", x.Id, x.ClientId.ToString(), x.FixedIncomeId, x.Description, x.Category, Money(x.Amount), x.DueDay, x.Active);
                foreach (var x in await db.Incomes.AsNoTracking().ToListAsync(ct)) await Row("Receita", x.Id, x.ClientId.ToString(), Ticks(x.ReferenceMonth), x.Payer, x.Description, x.Type, Money(x.Amount), x.FixedIncomeId, Ticks(x.GeneratedForMonth));
                foreach (var x in await db.Expenses.AsNoTracking().Include(x => x.Payments).ToListAsync(ct)) await Row("Despesa", x.Id, x.ClientId.ToString(), x.IncomeId, Ticks(x.CreatedOn), Ticks(x.DueDate), x.InstallmentNumber, x.Description, x.Category, Money(x.Amount), x.Payments.Sum(p => p.Amount) >= x.Amount ? 1 : x.Payments.Count > 0 ? 0 : null, x.Replicable, x.FixedExpenseId, Ticks(x.GeneratedForMonth));
                foreach (var x in await db.Payments.AsNoTracking().ToListAsync(ct)) await Row("Transacao", x.Id, x.ClientId.ToString(), x.ExpenseId, x.Note, Money(x.Amount), Ticks(x.Date));
                foreach (var x in await db.MonthlyPreparationItems.AsNoTracking().ToListAsync(ct)) await Row("PreparacaoMensalItem", x.Id, x.ClientId.ToString(), x.ModelType == "INCOME" ? "RECEITA" : "DESPESA", x.ModelId, Ticks(x.ReferenceMonth), x.State == "GENERATED" ? "GERADO" : "IGNORADO", x.UpdatedAtUtc.Ticks);
                await write.CommitAsync(ct);
            }
            await read.CommitAsync(ct);
            return await File.ReadAllBytesAsync(path, ct);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
    private static long? Ticks(DateOnly? date) => date?.ToDateTime(TimeOnly.MinValue).Ticks;
    private static string Money(decimal value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
