using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PersonalFinance.Api.Services;

public static class ImportEndpoint
{
    public static async Task<IResult> HandleAsync(
        IFormFile file, SqliteImporter importer, HttpContext context,
        ILogger<SqliteImporter> logger, CancellationToken ct)
    {
        try
        {
            return Results.Ok(await importer.ImportAsync(file, ct));
        }
        catch (ImportConflictException exception)
        {
            return Results.Problem(statusCode: 409, title: "Importação indisponível", detail: exception.Message);
        }
        catch (InvalidDataException exception)
        {
            return Results.Problem(statusCode: 400, title: "Backup inválido", detail: exception.Message);
        }
        catch (SqliteException exception)
        {
            logger.LogWarning(exception, "Falha ao ler backup SQLite. Referência {TraceId}", context.TraceIdentifier);
            return Results.Problem(statusCode: 400, title: "Backup inválido",
                detail: "Não foi possível ler este arquivo SQLite. Exporte novamente o backup pelo aplicativo Android.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha na importação SQLite. Referência {TraceId}", context.TraceIdentifier);
            return Results.Problem(statusCode: 500, title: "Falha na importação",
                detail: $"O servidor não conseguiu concluir a importação. Consulte o log do MonsterASP. Referência: {context.TraceIdentifier}");
        }
    }
}
