using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Data;
using PersonalFinance.Contracts;

namespace PersonalFinance.Api.Services;

public sealed class SqliteImporter(FinanceDbContext db)
{
    public async Task<ImportResult> ImportAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > 100 * 1024 * 1024) throw new InvalidDataException("O backup está vazio ou excede 100 MB.");
        var temporary = Path.Combine(Path.GetTempPath(), $"personal-finance-{Guid.NewGuid():N}.db");
        try
        {
            await using (var output = File.Create(temporary)) await file.CopyToAsync(output, ct);
            await using var source = new SqliteConnection($"Data Source={temporary};Mode=ReadOnly"); await source.OpenAsync(ct);
            await ValidateAsync(source, ct);
            // SQL Server retries must include the entire explicit transaction.
            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
                if (await db.Incomes.AnyAsync(ct) || await db.Expenses.AnyAsync(ct) ||
                    await db.Payments.AnyAsync(ct) || await db.FixedIncomes.AnyAsync(ct) ||
                    await db.FixedExpenses.AnyAsync(ct) || await db.MonthlyPreparationItems.AnyAsync(ct))
                    throw new ImportConflictException("Já existem lançamentos financeiros neste banco. A importação exige um banco financeiro vazio; a conta de login pode permanecer.");
                var fixedIncomeMap = await ImportRowsAsync(source, "ReceitaFixa", row => new FixedIncome
                {
                    Payer = row.Text("FontePagadora"),
                    Description = row.Text("Descricao"),
                    Type = row.Text("Tipo"),
                    Amount = row.Decimal("Valor"),
                    Active = row.Bool("Ativa", true)
                }, ct);
                var incomeMap = await ImportRowsAsync(source, "Receita", row => new Income
                {
                    ReferenceMonth = row.Date("MesReferencia"),
                    Payer = row.Text("FontePagadora"),
                    Description = row.Text("Descricao"),
                    Type = row.Text("Tipo"),
                    Amount = row.Decimal("Valor"),
                    FixedIncomeId = OptionalId(fixedIncomeMap, row.NullableInt("ReceitaFixaId")),
                    GeneratedForMonth = row.NullableDate("CompetenciaGeracao")
                }, ct);
                var fixedExpenseMap = await ImportRowsAsync(source, "DespesaFixa", row => new FixedExpense
                {
                    FixedIncomeId = RequiredId(fixedIncomeMap, row.Int("ReceitaFixaId"), "receita fixa", row),
                    Description = row.Text("Descricao"),
                    Category = row.Text("Categoria"),
                    Amount = row.Decimal("Valor"),
                    DueDay = row.Int("DiaVencimento", 1),
                    Active = row.Bool("Ativa", true)
                }, ct);
                var expenseMap = await ImportRowsAsync(source, "Despesa", row => new Expense
                {
                    IncomeId = RequiredId(incomeMap, row.Int("ReceitaId"), "receita", row),
                    CreatedOn = row.Date("DataCadastro"),
                    DueDate = row.Date("Vencimento"),
                    InstallmentNumber = row.Int("NParcela"),
                    Description = row.Text("Descricao"),
                    Category = row.Text("Categoria"),
                    Amount = row.Decimal("Valor"),
                    Replicable = row.Bool("replicavel"),
                    FixedExpenseId = OptionalId(fixedExpenseMap, row.NullableInt("DespesaFixaId")),
                    GeneratedForMonth = row.NullableDate("CompetenciaGeracao")
                }, ct);
                var paymentMap = await ImportRowsAsync(source, "Transacao", row => new Payment
                {
                    ExpenseId = RequiredId(expenseMap, row.Int("DespesaId"), "despesa", row),
                    Note = row.Text("Observacao"),
                    Amount = row.Decimal("Valor"),
                    Date = row.Date("Data")
                }, ct);
                await ImportRowsAsync(source, "PreparacaoMensalItem", row => new MonthlyPreparationItem
                {
                    ModelType = row.Text("TipoModelo") == "RECEITA" ? "INCOME" : "EXPENSE",
                    ModelId = row.Text("TipoModelo") == "RECEITA" ? RequiredId(fixedIncomeMap, row.Int("ModeloId"), "receita fixa", row) : RequiredId(fixedExpenseMap, row.Int("ModeloId"), "despesa fixa", row),
                    ReferenceMonth = row.Date("Competencia"), State = row.Text("Estado") == "GERADO" ? "GENERATED" : "IGNORED"
                }, ct);
                await db.SaveChangesAsync(ct);
                var result = new ImportResult(incomeMap.Count, expenseMap.Count, paymentMap.Count, fixedIncomeMap.Count, fixedExpenseMap.Count, await db.Incomes.SumAsync(x => (decimal?)x.Amount, ct) ?? 0, await db.Expenses.SumAsync(x => (decimal?)x.Amount, ct) ?? 0, await db.Payments.SumAsync(x => (decimal?)x.Amount, ct) ?? 0);
                var sourceIncomes = await RowsAsync(source, "Receita", ct);
                var sourceExpenses = await RowsAsync(source, "Despesa", ct);
                var sourcePayments = await RowsAsync(source, "Transacao", ct);
                if (result.Incomes != sourceIncomes.Count || result.Expenses != sourceExpenses.Count ||
                    result.Payments != sourcePayments.Count || result.IncomeTotal != sourceIncomes.Sum(x => x.Decimal("Valor")) ||
                    result.ExpenseTotal != sourceExpenses.Sum(x => x.Decimal("Valor")) ||
                    result.PaymentTotal != sourcePayments.Sum(x => x.Decimal("Valor")))
                    throw new InvalidDataException("As contagens ou os valores importados diferem do backup. A importação foi cancelada.");
                await transaction.CommitAsync(ct);
                return result;
            });
        }
        finally
        {
            // Cleanup must not mask the original import failure.
            try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }


    private async Task<Dictionary<int, T>> ImportRowsAsync<T>(
        SqliteConnection source, string table, Func<Row, T> convert, CancellationToken ct)
        where T : FinancialRecord
    {
        var map = new Dictionary<int, T>();
        foreach (var row in await RowsAsync(source, table, ct))
        {
            var record = convert(row);
            record.ClientId = Guid.TryParse(row.Text("ClientId"), out var identity) && identity != Guid.Empty ? identity : Guid.NewGuid();
            record.CreatedAtUtc = DateTime.UtcNow;
            record.UpdatedAtUtc = record.CreatedAtUtc;
            var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(record,
                new System.ComponentModel.DataAnnotations.ValidationContext(record), errors, validateAllProperties: true))
                throw new InvalidDataException($"Registro {table} {row.Int("Id")} inválido: {string.Join("; ", errors.Select(x => x.ErrorMessage))}");
            if (!map.TryAdd(row.Int("Id"), record))
                throw new InvalidDataException($"ID duplicado na tabela {table}.");
        }
        if (map.Count > 0)
        {
            db.Set<T>().AddRange(map.Values);
            await db.SaveChangesAsync(ct);
        }
        return map;
    }

    private static int RequiredId<T>(Dictionary<int, T> map, int oldId, string relation, Row row)
        where T : FinancialRecord =>
        map.TryGetValue(oldId, out var record) ? record.Id :
        throw new InvalidDataException($"O registro {row.Int("Id")} referencia uma {relation} inexistente no backup.");

    private static int? OptionalId<T>(Dictionary<int, T> map, int? oldId) where T : FinancialRecord =>
        oldId.HasValue && map.TryGetValue(oldId.Value, out var record) ? record.Id : null;

    private static async Task ValidateAsync(SqliteConnection source, CancellationToken ct)
    {
        await using var integrity = source.CreateCommand(); integrity.CommandText = "PRAGMA integrity_check"; var result = (string?)await integrity.ExecuteScalarAsync(ct);
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("O arquivo SQLite está corrompido.");
        var requiredColumns = new Dictionary<string, string[]>
        {
            ["Receita"] = ["Id", "MesReferencia", "Valor"],
            ["Despesa"] = ["Id", "ReceitaId", "DataCadastro", "Vencimento", "Valor"],
            ["Transacao"] = ["Id", "DespesaId", "Data", "Valor"]
        };
        foreach (var (table, required) in requiredColumns)
        {
            await using var command = source.CreateCommand();
            command.CommandText = $"PRAGMA table_info([{table}])";
            await using var reader = await command.ExecuteReaderAsync(ct);
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync(ct)) columns.Add(reader.GetString(1));
            if (required.Any(column => !columns.Contains(column)))
                throw new InvalidDataException($"A tabela {table} não contém os campos esperados do aplicativo Android.");
        }
    }
    private static async Task<List<Row>> RowsAsync(SqliteConnection connection, string table, CancellationToken ct)
    {
        await using var exists = connection.CreateCommand(); exists.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=$name"; exists.Parameters.AddWithValue("$name", table); if (Convert.ToInt32(await exists.ExecuteScalarAsync(ct)) == 0) return [];
        await using var command = connection.CreateCommand(); command.CommandText = $"SELECT * FROM [{table}]"; await using var reader = await command.ExecuteReaderAsync(ct); var rows = new List<Row>(); while (await reader.ReadAsync(ct)) { var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase); for (var i = 0; i < reader.FieldCount; i++) values[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i); rows.Add(new Row(values)); }
        return rows;
    }
    private sealed class Row(Dictionary<string, object?> values)
    {
        public string Text(string name) => values.TryGetValue(name, out var v) ? Convert.ToString(v) ?? string.Empty : string.Empty;
        public int Int(string name, int fallback = 0) => values.TryGetValue(name, out var v) && v is not null ? Convert.ToInt32(v) : fallback;
        public int? NullableInt(string name) => values.TryGetValue(name, out var v) && v is not null ? Convert.ToInt32(v) : null;
        public decimal Decimal(string name) => values.TryGetValue(name, out var v) && v is not null ? Convert.ToDecimal(v, System.Globalization.CultureInfo.InvariantCulture) : 0;
        public bool Bool(string name, bool fallback = false) => values.TryGetValue(name, out var v) && v is not null ? Convert.ToInt32(v) != 0 : fallback;
        public DateOnly Date(string name) => NullableDate(name) ?? throw new InvalidDataException($"Data {name} inválida no backup.");
        public DateOnly? NullableDate(string name)
        {
            if (!values.TryGetValue(name, out var value) || value is null) return null;
            if (value is DateTime date) return DateOnly.FromDateTime(date);
            // sqlite-net-pcl stores DateTime as .NET ticks by default.
            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            {
                if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                    throw new InvalidDataException($"Data {name} inválida no backup.");
                return DateOnly.FromDateTime(new DateTime(ticks));
            }
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return DateOnly.FromDateTime(date);
            throw new InvalidDataException($"Data {name} inválida no backup.");
        }
    }
}
