using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Android.App;
using Android.Content;
using Android.Net;
using PersonalFinance.Contracts;
using PersonalFinance.Resources.Widgets;

namespace PersonalFinance.Resources.Services;

public static class AndroidSync
{
    public static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { TypeInfoResolver = FinanceJsonContext.Default };
    private static ISharedPreferences Preferences => Application.Context.GetSharedPreferences("finance-sync", FileCreationMode.Private)!;
    public static string Url => Preferences.GetString("url", "https://miguelfagner.runasp.net/")!;
    public static string Email => Preferences.GetString("email", "")!;
    public static string? LastError { get; private set; }
    public static event Action? Updated;
    public static bool Connected => ((ConnectivityManager?)Application.Context.GetSystemService(Context.ConnectivityService))?.ActiveNetworkInfo?.IsConnected == true;
    public static async Task LoginAsync(string url, string email, string password)
    {
        if (!System.Uri.TryCreate(url, UriKind.Absolute, out var address) || address.Scheme != "https") throw new ArgumentException("Informe um endereço HTTPS válido.");
        if (!url.EndsWith('/')) url += "/";
        // A changed account/server starts a new initial transfer.
        using var http = new HttpClient { BaseAddress = new System.Uri(url), Timeout = TimeSpan.FromSeconds(60) };
        using var response = await http.PostAsJsonAsync("api/auth/login", new LoginRequest { Email=email, Password=password });
        await CheckAsync(response);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json) ?? throw new InvalidOperationException("Resposta de login inválida.");
        await Gate.WaitAsync();
        try
        {
            if (url != Url || email != Email) await new DatabaseService().MarkReconciliationAsync();
            TokenVault.Save(login);
            Preferences.Edit()!.PutString("url",url)!.PutString("email",email)!.Apply();
        }
        finally { Gate.Release(); }
        await InitializeFromDeviceAsync();
        await SyncAsync();
        SyncJob.Schedule();
    }
    public static void Logout()
    {
        var refresh = TokenVault.ReadRefresh();
        TokenVault.Clear(); LastError = null;
        if (!string.IsNullOrWhiteSpace(refresh)) _ = RevokeAsync(refresh);
    }
    private static async Task RevokeAsync(string refresh)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new System.Uri(Url), Timeout = TimeSpan.FromSeconds(12) };
            await http.PostAsJsonAsync("api/auth/logout", new RefreshRequest(refresh), Json);
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
    }
    public static async Task<HttpClient> ClientAsync()
    {
        await RefreshGate.WaitAsync();
        try
        {
            var refresh = TokenVault.ReadRefresh();
            var expiry = TokenVault.ReadExpiry();
            if (!string.IsNullOrWhiteSpace(refresh) && (!expiry.HasValue || expiry.Value <= DateTime.UtcNow.AddMinutes(1)))
            {
                using var http = new HttpClient { BaseAddress = new System.Uri(Url), Timeout = TimeSpan.FromSeconds(12) };
                using var response = await http.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(refresh), Json);
                if (!response.IsSuccessStatusCode) throw new UnauthorizedAccessException("Entre novamente; seus dados locais foram preservados.");
                var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json) ?? throw new UnauthorizedAccessException();
                TokenVault.Save(login);
            }
            return Client();
        }
        finally { RefreshGate.Release(); }
    }
    public static HttpClient Client()
    {
        var token = TokenVault.Read();
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedAccessException("Entre na sua conta para sincronizar.");
        var http = new HttpClient { BaseAddress=new System.Uri(Url), Timeout=TimeSpan.FromSeconds(120) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token);
        return http;
    }
    public static async Task<SyncStatus> StatusAsync()
    {
        using var http=await ClientAsync();using var response=await http.GetAsync("api/sync/status");await CheckAsync(response);
        return (await response.Content.ReadFromJsonAsync<SyncStatus>(Json))!;
    }
    public static async Task<List<SyncChange>> RemoteSnapshotAsync()
    {
        using var http=await ClientAsync();long cursor=0;var latest=new Dictionary<(string,Guid),SyncChange>();
        SyncPullResult page;
        do { using var response=await http.GetAsync($"api/sync/pull?after={cursor}");await CheckAsync(response);page=(await response.Content.ReadFromJsonAsync<SyncPullResult>(Json))!;foreach(var c in page.Changes)latest[(c.EntityType,c.ClientId)]=c;cursor=page.Cursor; }while(page.HasMore);
        return latest.Values.Where(x=>!x.Deleted).ToList();
    }
    private static async Task InitializeFromDeviceAsync()
    {
        await Gate.WaitAsync();
        try
        {
            var db = new DatabaseService();
            var state = await db.GetSyncStateAsync();
            if (state.Initialized && !state.NeedsReconciliation) return;
            var directory = Application.Context.FilesDir!.AbsolutePath;
            var path = Path.Combine(directory, "sync-from-device.json");
            DeviceSnapshotCheckpoint? checkpoint = null;
            if (File.Exists(path))
            {
                checkpoint = JsonSerializer.Deserialize<DeviceSnapshotCheckpoint>(await File.ReadAllTextAsync(path), Json)!;
                if (checkpoint.Server != Url || checkpoint.Email != Email)
                { File.Move(path, path + "." + Guid.NewGuid().ToString("N")); checkpoint = null; }
            }
            if (checkpoint is null)
            {
                // An empty new installation receives the Web history instead of clearing it.
                if ((await db.SnapshotAsync()).Count == 0)
                {
                    await db.ReplaceSnapshotAsync(await RemoteSnapshotAsync());
                    using var client = await ClientAsync();
                    await PullAsync(client, db);
                    LastError = null;
                    return;
                }
                await db.CreateConsistentBackupAsync(Path.Combine(directory, $"financeiro_before_sync_{DateTime.Now:yyyyMMdd_HHmmss_fff}.db"));
                checkpoint = await db.CaptureDeviceSnapshotAsync((await StatusAsync()).Cursor, Url, Email);
                // Exact request survives a lost acknowledgement; retries cannot replace newer Web edits.
                var temporary = path + ".tmp";
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(checkpoint, Json));
                File.Move(temporary, path, true);
            }
            using var http = await ClientAsync();
            using var response = await http.PostAsJsonAsync("api/sync/from-device", checkpoint.Request, Json);
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest) File.Delete(path);
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new InvalidOperationException("Publique a nova versão do Web para ativar a primeira carga automática do celular.");
            await CheckAsync(response);
            var result = await response.Content.ReadFromJsonAsync<SyncPullResult>(Json)
                ?? throw new InvalidOperationException("O servidor não confirmou a primeira carga.");
            await db.CompleteDeviceSnapshotAsync(checkpoint, result);
            File.Delete(path);
            LastError = null;
            Updated?.Invoke();
            await PersonalFinanceWidgetProvider.AtualizarTodosAsync(Application.Context);
        }
        catch (Exception error) { LastError = error.Message; throw; }
        finally { Gate.Release(); }
    }
    public static async Task BootstrapAsync(long expectedCursor, bool reconciled, IReadOnlyDictionary<Guid,Guid>? mapping=null)
    {
        await Gate.WaitAsync();
        try
        {
            var db=new DatabaseService();
            var backup=Path.Combine(Application.Context.FilesDir!.AbsolutePath,$"financeiro_before_sync_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            await db.CreateConsistentBackupAsync(backup);
            var pending=await db.FreezePendingAsync(true);
            var changes=pending.Select(x=>JsonSerializer.Deserialize<SyncChange>(x.Payload!,Json)!).ToList();
            // After restoring a backup, include unchanged records as well as its captured changes.
            var represented=changes.Select(x=>(x.EntityType,x.ClientId)).ToHashSet();
            changes.AddRange((await db.SnapshotAsync()).Where(x=>!represented.Contains((x.EntityType,x.ClientId))));
            if(mapping is not null)
                changes=changes.Select(c=>{
                    var id=mapping.GetValueOrDefault(c.ClientId,c.ClientId);
                    var data=c.Data;
                    if(data.HasValue){var node=System.Text.Json.Nodes.JsonNode.Parse(data.Value.GetRawText())!;node["clientId"]=id.ToString();data=JsonSerializer.SerializeToElement(node,Json);}
                    return c with {ClientId=id,Data=data,ParentClientId=c.ParentClientId.HasValue?mapping.GetValueOrDefault(c.ParentClientId.Value,c.ParentClientId.Value):null,FixedClientId=c.FixedClientId.HasValue?mapping.GetValueOrDefault(c.FixedClientId.Value,c.FixedClientId.Value):null};
                }).ToList();
            using var http=await ClientAsync();
            // Persist the exact request so a lost acknowledgement can be safely retried.
            var requestPath=Path.Combine(Application.Context.FilesDir.AbsolutePath,"sync-bootstrap.json");
            BootstrapCheckpoint checkpoint;
            if(File.Exists(requestPath))
            {
                checkpoint=JsonSerializer.Deserialize<BootstrapCheckpoint>(await File.ReadAllTextAsync(requestPath),Json)!;
                if(checkpoint.Server!=Url||checkpoint.Email!=Email){File.Move(requestPath,requestPath+"."+Guid.NewGuid().ToString("N"));checkpoint=new(new(Guid.NewGuid(),changes,expectedCursor,reconciled),mapping?.ToDictionary()??[],Url,Email);}
            }
            else checkpoint=new(new(Guid.NewGuid(),changes,expectedCursor,reconciled),mapping?.ToDictionary()??[],Url,Email);
            await File.WriteAllTextAsync(requestPath,JsonSerializer.Serialize(checkpoint,Json));
            var request=checkpoint.Request;
            var remoteBefore = request.Reconciled ? await RemoteSnapshotAsync() : [];
            var remoteByIdentity = remoteBefore.ToDictionary(x => (x.EntityType,x.ClientId));
            using var response=await http.PostAsJsonAsync("api/sync/bootstrap",request,Json);
            if(response.StatusCode==HttpStatusCode.Conflict||response.StatusCode==HttpStatusCode.BadRequest)File.Delete(requestPath);
            await CheckAsync(response);
            var result=(await response.Content.ReadFromJsonAsync<SyncPushResult>(Json))!;
            if(result.Error is not null)throw new InvalidOperationException(result.Error);
            foreach(var change in request.Changes)
                if(remoteByIdentity.TryGetValue((change.EntityType,change.ClientId),out var original) &&
                    DifferentFinancialData(change,original))
                    await db.ArchiveBootstrapConflictAsync(change);
            // Apply chosen identity associations locally before reading the server feed.
            await db.RemapIdentitiesAsync(checkpoint.Mapping);
            await db.CompleteBootstrapAsync(result.Accepted);File.Delete(requestPath);
            await PullAsync(http,db);
            LastError=null;await PersonalFinanceWidgetProvider.AtualizarTodosAsync(Application.Context);
        }
        catch(Exception e){LastError=e.Message;throw;}
        finally{Gate.Release();}
    }
    private static bool DifferentFinancialData(SyncChange local, SyncChange remote)
    {
        if(local.Deleted!=remote.Deleted)return true;
        if(!local.Data.HasValue||!remote.Data.HasValue)return local.Data.HasValue!=remote.Data.HasValue;
        string[] fields = local.EntityType switch
        {
            "Income" => ["referenceMonth","payer","description","type","amount","generatedForMonth"],
            "Expense" => ["createdOn","dueDate","installmentNumber","description","category","amount","replicable","generatedForMonth"],
            "Payment" => ["date","note","amount"],
            "FixedIncome" => ["payer","description","type","amount","active"],
            "FixedExpense" => ["description","category","amount","dueDay","active"],
            "MonthlyPreparationItem" => ["modelType","referenceMonth","state"],
            _ => []
        };
        foreach(var field in fields)
        {
            var left=local.Data.Value.TryGetProperty(field,out var a)?a.ToString():"";
            var right=remote.Data.Value.TryGetProperty(field,out var b)?b.ToString():"";
            if(!string.Equals(left,right,StringComparison.Ordinal))return true;
        }
        return false;
    }
    public static async Task SyncAsync()
    {
        if (!Connected || TokenVault.Read() is null) return;
        try
        {
            var state = await new DatabaseService().GetSyncStateAsync();
            if (!state.Initialized || state.NeedsReconciliation) await InitializeFromDeviceAsync();
        }
        catch (Exception error) { LastError = error.Message; return; }
        if (!await Gate.WaitAsync(0)) return;
        try
        {
            var db=new DatabaseService();var state=await db.GetSyncStateAsync();
            if(!state.Initialized||state.NeedsReconciliation)return;
            using var http=await ClientAsync();
            while(true)
            {
                var pending=await db.FreezePendingAsync();if(pending.Count==0)break;
                var changes=pending.Take(1).Select(x=>JsonSerializer.Deserialize<SyncChange>(x.Payload!,Json)!).ToList();
                using var response=await http.PostAsJsonAsync("api/sync/push",new SyncPushRequest(changes),Json);await CheckAsync(response);
                var result=(await response.Content.ReadFromJsonAsync<SyncPushResult>(Json))!;
                await db.AcceptPendingAndRebaseAsync(result);
                foreach(var conflict in result.Conflicts)await db.ArchiveConflictAsync(conflict);
                if(result.Error is not null&&result.Conflicts.Count==0)throw new InvalidOperationException(result.Error);
                if(result.Conflicts.Count>0)continue;
                if(result.Accepted.Count==0)break;
            }
            while(true)
            {
                var pendingCommand=(await db.PendingCommandsAsync(Url,Email)).FirstOrDefault();
                if(pendingCommand is null)break;
                var command=JsonSerializer.Deserialize<FinanceCommand>(pendingCommand.Payload,Json)!;
                using var response=await http.PostAsJsonAsync("api/commands",command,Json);
                if(response.StatusCode==HttpStatusCode.Conflict)
                {
                    var conflict=await response.Content.ReadFromJsonAsync<FinanceCommandResult>(Json);
                    if(conflict?.Status=="Conflict")
                    {
                        await db.ArchiveCommandConflictAsync(pendingCommand,conflict.Error??"O servidor possui uma versão mais recente.",conflict.Current);
                        continue;
                    }
                }
                await CheckAsync(response);
                var result=await response.Content.ReadFromJsonAsync<FinanceCommandResult>(Json);
                if(result?.Status!="Applied")throw new InvalidOperationException("O servidor não confirmou a operação.");
                await db.ApplyConfirmedCommandAsync(command,result);
                await db.AcceptCommandAndRebaseAsync(pendingCommand.OperationId,result.Revisions);
            }
            await PullAsync(http,db);
            LastError=null;await PersonalFinanceWidgetProvider.AtualizarTodosAsync(Application.Context);
        }
        catch(Exception e){LastError=e is HttpRequestException?"Sem conexão com o servidor. Pendências preservadas.":e.Message;}
        finally{Gate.Release();}
    }
    private static async Task PullAsync(HttpClient http,DatabaseService db)
    {
        SyncPullResult page;
        var changed=false;
        do{var state=await db.GetSyncStateAsync();using var response=await http.GetAsync($"api/sync/pull?after={state.Cursor}");await CheckAsync(response);page=(await response.Content.ReadFromJsonAsync<SyncPullResult>(Json))!;await db.ApplyPullAsync(page);changed|=page.Changes.Count>0;}while(page.HasMore);
        if(changed)Updated?.Invoke();
    }
    private static async Task CheckAsync(HttpResponseMessage response)
    {
        if(response.StatusCode==HttpStatusCode.Unauthorized)throw new UnauthorizedAccessException("Sua sessão expirou. Entre novamente; seus dados locais foram preservados.");
        if(response.IsSuccessStatusCode)return;
        string? detail=null;try{var error=await response.Content.ReadFromJsonAsync<JsonElement>();if(error.TryGetProperty("detail",out var d))detail=d.GetString();}catch(JsonException){}
        throw new InvalidOperationException(detail??$"Falha ao sincronizar (HTTP {(int)response.StatusCode}).");
    }
}
