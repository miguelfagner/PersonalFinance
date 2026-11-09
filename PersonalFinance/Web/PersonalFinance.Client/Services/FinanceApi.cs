using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using PersonalFinance.Contracts;

namespace PersonalFinance.Client.Services;

public sealed class FinanceApi(HttpClient http, BrowserStorage storage)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private string? accessToken;
    private DateTime expiresAtUtc;
    public async Task<bool> LoginAsync(LoginRequest request)
    {
        var result = await http.PostAsJsonAsync("api/auth/login", request);
        if (!result.IsSuccessStatusCode) return false;
        var login = await result.Content.ReadFromJsonAsync<LoginResponse>();
        if (login is null) return false;
        accessToken = login.Token;
        expiresAtUtc = login.ExpiresAt;
        await storage.SetAsync("subject", await storage.SubjectAsync(login.Token));
        if (!string.IsNullOrWhiteSpace(login.RefreshToken)) await storage.SetAsync("refreshToken", login.RefreshToken);
        await storage.RemoveAsync("token");
        return true;
    }
    public async Task<bool> IsLoggedInAsync() => accessToken is not null || !string.IsNullOrEmpty(await storage.GetAsync("refreshToken"))
        || !string.IsNullOrEmpty(await storage.GetAsync("token"));
    public async Task LogoutAsync()
    {
        var refresh = await storage.GetAsync("refreshToken");
        if (!string.IsNullOrWhiteSpace(refresh))
        {
            try { await http.PostAsJsonAsync("api/auth/logout", new RefreshRequest(refresh), Json); }
            catch (HttpRequestException) { }
        }
        accessToken = null;
        await storage.RemoveAsync("token");
        await storage.RemoveAsync("refreshToken");
        await storage.RemoveAsync("subject");
    }
    public async Task<string> OwnerAsync()
    {
        var subject = await storage.GetAsync("subject");
        if (string.IsNullOrWhiteSpace(subject))
        {
            var legacyToken = await storage.GetAsync("token") ?? accessToken;
            if (!string.IsNullOrWhiteSpace(legacyToken)) subject = await storage.SubjectAsync(legacyToken);
            if (!string.IsNullOrWhiteSpace(subject)) await storage.SetAsync("subject", subject);
        }
        if (string.IsNullOrWhiteSpace(subject)) throw new UnauthorizedAccessException("Entre novamente para vincular as pendências à sua conta.");
        return $"{http.BaseAddress?.AbsoluteUri}|{subject}";
    }
    public async Task<string[]> PendingAsync() => await storage.PendingAsync(await OwnerAsync());
    public async Task RemovePendingAsync(string id) => await storage.RemovePendingAsync(await OwnerAsync(), id);
    public async Task RetryPendingAsync(string id) => await storage.MarkPendingAsync(await OwnerAsync(), id, "queued", null, null);
    public ValueTask<int> LegacyPendingCountAsync() => storage.LegacyCountAsync();
    public async Task DownloadBackupAsync()
    {
        await AuthorizeAsync();
        var token = accessToken ?? throw new UnauthorizedAccessException();
        await storage.DownloadBackupAsync(http.BaseAddress!.AbsoluteUri, token);
    }
    private async Task AuthorizeAsync()
    {
        await refreshGate.WaitAsync();
        try
        {
            if (accessToken is null)
            {
                accessToken = await storage.GetAsync("token");
                if (accessToken is not null) await storage.RemoveAsync("token");
            }
            if (accessToken is null || expiresAtUtc <= DateTime.UtcNow.AddMinutes(1))
            {
                var refresh = await storage.GetAsync("refreshToken");
                if (!string.IsNullOrWhiteSpace(refresh))
                {
                    using var result = await http.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(refresh), Json);
                    if (!result.IsSuccessStatusCode) throw new UnauthorizedAccessException("Entre novamente; as pendências foram preservadas.");
                    var session = await result.Content.ReadFromJsonAsync<LoginResponse>(Json)
                        ?? throw new UnauthorizedAccessException();
                    accessToken = session.Token;
                    expiresAtUtc = session.ExpiresAt;
                    if (!string.IsNullOrWhiteSpace(session.RefreshToken)) await storage.SetAsync("refreshToken", session.RefreshToken);
                }
            }
            http.DefaultRequestHeaders.Authorization = accessToken is null ? null : new AuthenticationHeaderValue("Bearer", accessToken);
        }
        finally { refreshGate.Release(); }
    }
    public async Task<T?> GetAsync<T>(string url)
    {
        await AuthorizeAsync(); var response = await http.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException();
        await CheckAsync(response); return await response.Content.ReadFromJsonAsync<T>(Json);
    }
    public async Task<bool> SendAsync<T>(HttpMethod method, string url, T value, FinancialRecord? target = null)
    {
        var outcome = await SendWithResultAsync<T, JsonElement>(method, url, value, target);
        return outcome.Sent;
    }
    public async Task<CommandOutcome<TResult>> SendWithResultAsync<T, TResult>(HttpMethod method, string url, T value, FinancialRecord? target = null)
    {
        var owner = await OwnerAsync();
        var command = MakeCommand(method, url, value, target);
        await storage.QueueAsync(owner, command);
        if (await storage.IsOnlineAsync()) await SynchronizeAsync();
        var raw = await storage.PendingItemAsync(owner, command.OperationId.ToString());
        var queued = raw is null ? null : JsonSerializer.Deserialize<QueuedCommand>(raw, Json);
        if (queued?.Status == "applied")
        {
            var data = queued.Result?.Data;
            var result = data.HasValue && data.Value.ValueKind != JsonValueKind.Null ? data.Value.Deserialize<TResult>(Json) : default;
            await storage.RemovePendingAsync(owner, command.OperationId.ToString());
            return new(true, result);
        }
        if (queued?.Status is "blocked" or "conflict") throw new InvalidOperationException(queued.Error ?? "A alteração precisa de revisão.");
        return new(false, default);
    }
    private static FinanceCommand MakeCommand<T>(HttpMethod method, string url, T value, FinancialRecord? target)
    {
        var data = JsonSerializer.SerializeToElement(value, Json);
        if (method == HttpMethod.Delete)
        {
            if (target is null) throw new ArgumentException("Selecione o lançamento a excluir.");
            return new(Guid.NewGuid(), "Delete", target.GetType().Name, target.ClientId, target.Revision, null);
        }
        if (value is FinancialRecord record)
            return new(Guid.NewGuid(), "Upsert", record.GetType().Name, record.ClientId, record.Id == 0 ? 0 : record.Revision, data,
                record switch { Expense x => x.IncomeClientId, Payment x => x.ExpenseClientId, _ => null });
        if (value is PrepareMonthRequest prepare) return new(prepare.OperationId, "Prepare", "", Guid.Empty, null, data);
        if (value is SettleRequest settle && target is Expense expense) return new(settle.OperationId, "Settle", "Expense", expense.ClientId, null, data);
        if (value is PersonalExpenseRequest personal && target is Expense budget) return new(Guid.NewGuid(), "Personal", "Payment", personal.ClientId, null, data, budget.ClientId);
        if (value is ConvertFixedRequest) return new(Guid.NewGuid(), "Convert", "", Guid.Empty, null, data);
        throw new ArgumentException($"A operação {method} {url} não é reconhecida.");
    }
    public async Task<int> SynchronizeAsync()
    {
        await AuthorizeAsync();
        var owner = await OwnerAsync();
        var holder = Guid.NewGuid().ToString("N");
        if (!await storage.AcquireQueueAsync(owner, holder)) return 0;
        var sent = 0;
        try
        {
            while (true)
            {
                if (!await storage.RenewQueueAsync(owner, holder)) break;
                var raw = (await storage.ReadyPendingAsync(owner)).FirstOrDefault();
                if (raw is null) break;
                var queued = JsonSerializer.Deserialize<QueuedCommand>(raw, Json);
                if (queued is null) break;
                if (!await storage.IsOnlineAsync()) break;
                HttpResponseMessage response;
                try { response = await http.PostAsJsonAsync("api/commands", queued.Command, Json); }
                catch (HttpRequestException) { break; }
                catch (TaskCanceledException) { break; }
                using (response)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException();
                    if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests) break;
                    FinanceCommandResult? result = null;
                    try { result = await response.Content.ReadFromJsonAsync<FinanceCommandResult>(Json); } catch (JsonException) { }
                    if (response.IsSuccessStatusCode && result?.Status == "Applied")
                    {
                        await storage.RebaseQueueAsync(owner,queued.Id,result.Revisions);
                        await storage.MarkPendingAsync(owner, queued.Id, "applied", null, result);
                        sent++;
                        continue;
                    }
                    var error = result?.Error ?? $"O servidor recusou a alteração (HTTP {(int)response.StatusCode}).";
                    await storage.MarkPendingAsync(owner, queued.Id, result?.Status == "Conflict" ? "conflict" : "blocked", error, result);
                    continue;
                }
            }
        }
        finally { await storage.ReleaseQueueAsync(owner, holder); }
        return sent;
    }
    public async Task<ImportResult?> ImportAsync(IBrowserFile file)
    {
        await AuthorizeAsync();
        using var form = new MultipartFormDataContent();
        using var content = new StreamContent(file.OpenReadStream(100 * 1024 * 1024));
        form.Add(content, "file", file.Name);
        using var response = await http.PostAsync("api/import/sqlite", form);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Sua sessão expirou. Entre novamente antes de importar o backup.");
        if (!response.IsSuccessStatusCode)
        {
            var message = $"Falha na importação (HTTP {(int)response.StatusCode}). Consulte o log do servidor.";
            if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
            {
                try
                {
                    var problem = await response.Content.ReadFromJsonAsync<ImportProblem>(Json);
                    if (!string.IsNullOrWhiteSpace(problem?.Detail)) message = problem.Detail;
                }
                catch (JsonException) { }
            }
            throw new InvalidOperationException(message);
        }
        return await response.Content.ReadFromJsonAsync<ImportResult>(Json);
    }
    private sealed class ImportProblem { public string? Detail { get; set; } }
    private static async Task CheckAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException("Sua sessão expirou. Entre novamente; as pendências foram preservadas.");
        if (response.IsSuccessStatusCode) return;
        string? detail = null;
        try { detail = (await response.Content.ReadFromJsonAsync<ImportProblem>(Json))?.Detail; } catch (JsonException) { }
        // HTTP rejection is a business/server error, never an offline request.
        throw new InvalidOperationException(detail ?? $"Não foi possível concluir (HTTP {(int)response.StatusCode}). Tente novamente.");
    }
    public sealed record CommandOutcome<T>(bool Sent, T? Result);
    private sealed class QueuedCommand
    {
        public string Id { get; set; } = "";
        public string Status { get; set; } = "queued";
        public string? Error { get; set; }
        public FinanceCommand Command { get; set; } = default!;
        public FinanceCommandResult? Result { get; set; }
    }
}
