using Android.Content;
using PersonalFinance.Resources.Models;
using SQLite;

namespace PersonalFinance.Resources.Services
{
    internal static class DatabaseBackupService
    {
        private static readonly Dictionary<string, string[]> RequiredTables = new()
        {
            [nameof(Receita)] = [nameof(Receita.Id), nameof(Receita.MesReferencia), nameof(Receita.Valor)],
            [nameof(Despesa)] = [nameof(Despesa.Id), nameof(Despesa.ReceitaId), nameof(Despesa.Valor)],
            [nameof(Banco)] = [nameof(Banco.Id), nameof(Banco.Nome)],
            [nameof(Transacao)] = [nameof(Transacao.Id), nameof(Transacao.DespesaId), nameof(Transacao.Valor)]
        };

        private static string DatabasePath =>
            Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, "financeiro.db");

        internal static async Task ImportAsync(ContentResolver contentResolver, Android.Net.Uri source)
        {
            var filesDirectory = Android.App.Application.Context.FilesDir!.AbsolutePath;
            var importedDatabasePath = Path.Combine(filesDirectory, $"financeiro_import_{Guid.NewGuid():N}.db");
            var previousDatabasePath = Path.Combine(filesDirectory, $"financeiro_previous_{Guid.NewGuid():N}.db");
            var currentDatabaseExisted = File.Exists(DatabasePath);
            var replacementStarted = false;
            var keepPreviousDatabase = false;

            try
            {
                using (var input = contentResolver.OpenInputStream(source)
                    ?? throw new IOException("Não foi possível abrir o arquivo selecionado."))
                using (var output = File.Create(importedDatabasePath))
                {
                    await input.CopyToAsync(output);
                }

                ValidateBackup(importedDatabasePath);

                SQLiteAsyncConnection.ResetPool();

                if (currentDatabaseExisted)
                    File.Copy(DatabasePath, previousDatabasePath, true);

                DeleteDatabaseSidecars();
                replacementStarted = true;
                File.Copy(importedDatabasePath, DatabasePath, true);
                DeleteDatabaseSidecars();

                ValidateBackup(DatabasePath);
            }
            catch (Exception importException)
            {
                if (replacementStarted)
                {
                    try
                    {
                        SQLiteAsyncConnection.ResetPool();
                        DeleteDatabaseSidecars();

                        if (currentDatabaseExisted && File.Exists(previousDatabasePath))
                            File.Copy(previousDatabasePath, DatabasePath, true);
                        else
                            TryDelete(DatabasePath);
                    }
                    catch (Exception restoreException)
                    {
                        keepPreviousDatabase = true;
                        throw new IOException(
                            "A importação falhou e não foi possível restaurar automaticamente o banco anterior.",
                            new AggregateException(importException, restoreException));
                    }
                }

                throw;
            }
            finally
            {
                TryDelete(importedDatabasePath);

                if (!keepPreviousDatabase)
                    TryDelete(previousDatabasePath);
            }
        }

        private static void ValidateBackup(string backupPath)
        {
            try
            {
                using var connection = new SQLiteConnection(backupPath, SQLiteOpenFlags.ReadOnly);

                var integrityResult = connection.ExecuteScalar<string>("PRAGMA integrity_check");
                if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("O arquivo de backup está corrompido.");

                foreach (var requiredTable in RequiredTables)
                {
                    var columns = connection.GetTableInfo(requiredTable.Key)
                        .Select(column => column.Name)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    if (requiredTable.Value.Any(column => !columns.Contains(column)))
                        throw new InvalidDataException("O arquivo selecionado não pertence a este aplicativo.");
                }
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "O arquivo selecionado não é um backup válido do aplicativo.",
                    exception);
            }
        }

        private static void DeleteDatabaseSidecars()
        {
            DeleteIfExists($"{DatabasePath}-journal");
            DeleteIfExists($"{DatabasePath}-shm");
            DeleteIfExists($"{DatabasePath}-wal");
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Arquivos temporários e auxiliares não devem impedir a restauração.
            }
        }
    }
}
