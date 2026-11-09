using Microsoft.JSInterop;

namespace PersonalFinance.Client.Services;

public sealed class BrowserStorage(IJSRuntime js)
{
    public ValueTask<string?> GetAsync(string key) => js.InvokeAsync<string?>("financeStorage.get", key);
    public ValueTask SetAsync(string key, string value) => js.InvokeVoidAsync("financeStorage.set", key, value);
    public ValueTask RemoveAsync(string key) => js.InvokeVoidAsync("financeStorage.remove", key);
    public ValueTask<string> SubjectAsync(string token) => js.InvokeAsync<string>("financeStorage.subject", token);
    public ValueTask<bool> IsOnlineAsync() => js.InvokeAsync<bool>("financeQueue.online");
    public ValueTask QueueAsync(string owner, object command) => js.InvokeVoidAsync("financeQueue.add", owner, command);
    public ValueTask<string[]> PendingAsync(string owner) => js.InvokeAsync<string[]>("financeQueue.list", owner);
    public ValueTask<string[]> ReadyPendingAsync(string owner) => js.InvokeAsync<string[]>("financeQueue.ready", owner);
    public ValueTask<string?> PendingItemAsync(string owner, string id) => js.InvokeAsync<string?>("financeQueue.get", owner, id);
    public ValueTask MarkPendingAsync(string owner, string id, string status, string? error, object? result) => js.InvokeVoidAsync("financeQueue.mark", owner, id, status, error, result);
    public ValueTask RemovePendingAsync(string owner, string id) => js.InvokeVoidAsync("financeQueue.remove", owner, id);
    public ValueTask<bool> AcquireQueueAsync(string owner, string holder) => js.InvokeAsync<bool>("financeQueue.acquire", owner, holder);
    public ValueTask<bool> RenewQueueAsync(string owner, string holder) => js.InvokeAsync<bool>("financeQueue.renew", owner, holder);
    public ValueTask RebaseQueueAsync(string owner, string id, IReadOnlyDictionary<Guid,long> revisions) =>
        js.InvokeVoidAsync("financeQueue.rebase", owner, id, revisions);
    public ValueTask ReleaseQueueAsync(string owner, string holder) => js.InvokeVoidAsync("financeQueue.release", owner, holder);
    public ValueTask<int> LegacyCountAsync() => js.InvokeAsync<int>("financeQueue.legacyCount");
    public ValueTask DownloadBackupAsync(string baseUrl, string token) => js.InvokeVoidAsync("financeBackup.download", baseUrl, token);
}
