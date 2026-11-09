using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PersonalFinance.Contracts;

namespace PersonalFinance.Client.Services;

public abstract class FinancePage : ComponentBase, IAsyncDisposable
{
    [Inject] protected FinanceApi Api { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;
    [Inject] protected IJSRuntime Js { get; set; } = null!;
    protected DateTime MonthValue { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    protected DateOnly Month => DateOnly.FromDateTime(MonthValue);
    protected string? Error;
    protected string? Message;
    protected bool Loading;
    protected bool Busy;
    private readonly CancellationTokenSource stop = new();
    private Task? refresh;
    private DotNetObjectReference<FinancePage>? reference;
    private bool reloadRequested;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) { reference = DotNetObjectReference.Create(this); await Js.InvokeVoidAsync("financeQueue.watch", reference); }
    }
    [JSInvokable] public async Task ResumeAsync()
    {
        if (Busy || stop.IsCancellationRequested) return;
        try { await Api.SynchronizeAsync(); await ReloadAsync(); }
        catch (UnauthorizedAccessException) { Navigation.NavigateTo("/login"); }
        catch (Exception e) { Error = e.Message; }
        StateHasChanged();
    }
    protected static readonly string[] Categories = ["PESSOAL", "CASA", "MORADIA", "ALIMENTACAO", "FAMÍLIA", "SAUDE", "CARTÃO CRÉDITO", "EMPRESA", "TRANSPORTE"];
    protected IEnumerable<string> CategoryOptions(string? current) => Categories.Concat(string.IsNullOrWhiteSpace(current) ? [] : new[] { current }).Distinct(StringComparer.OrdinalIgnoreCase);
    protected string Link(string page) => $"/{page}?month={MonthValue:yyyy-MM}";
    protected string? Query(string name) => new Uri(Navigation.Uri).Query.TrimStart('?').Split('&').Select(x => x.Split('=', 2)).Where(x => x.Length == 2 && x[0] == name).Select(x => Uri.UnescapeDataString(x[1])).FirstOrDefault();
    protected override async Task OnInitializedAsync()
    {
        if (!await Api.IsLoggedInAsync()) { Navigation.NavigateTo("/login"); return; }
        if (DateTime.TryParseExact(Query("month"), "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)) MonthValue = date;
        await InitializeAsync(); await ReloadAsync(); refresh = RefreshAsync();
    }
    protected virtual Task InitializeAsync() => Task.CompletedTask;
    protected abstract Task LoadAsync();
    protected async Task ReloadAsync()
    {
        if (Loading || Busy) { reloadRequested = true; return; }
        Loading = true;
        try
        {
            DateOnly requestedMonth;
            do
            {
                reloadRequested = false;
                requestedMonth = Month;
                Error = null;
                try { await LoadAsync(); }
                catch (UnauthorizedAccessException) { Navigation.NavigateTo("/login"); return; }
                catch (Exception e) when (requestedMonth == Month)
                {
                    Error = e is InvalidOperationException ? e.Message : "Não foi possível carregar os dados. Confira sua conexão e tente novamente.";
                }
                catch (Exception) when (requestedMonth != Month) { }
            } while (reloadRequested || Month != requestedMonth);
        }
        finally { Loading = false; StateHasChanged(); }
    }
    protected async Task ChangeMonthAsync(int offset)
    {
        MonthValue = MonthValue.AddMonths(offset);
        var uri = new Uri(Navigation.Uri);
        var other = uri.Query.TrimStart('?').Split('&').Where(x => x.Length > 0 && !x.StartsWith("month="));
        Navigation.NavigateTo(uri.AbsolutePath + "?" + string.Join('&', other.Append($"month={MonthValue:yyyy-MM}")), replace: true);
        await ReloadAsync();
    }
    protected async Task RunAsync(Func<Task> action)
    {
        if (Busy) return;
        Busy = true; Error = null; Message = null;
        try { await action(); }
        catch (UnauthorizedAccessException) { Navigation.NavigateTo("/login"); }
        catch (Exception e) { Error = e is InvalidOperationException ? e.Message : "Não foi possível concluir. Confira sua conexão e tente novamente."; }
        finally { Busy = false; if (reloadRequested) await ReloadAsync(); }
    }
    protected async Task<bool> SaveAsync<T>(string url, T item, int id = 0, FinancialRecord? target = null)
    {
        var sent = await Api.SendAsync(id == 0 ? HttpMethod.Post : HttpMethod.Put, id == 0 ? url : $"{url}/{id}", item, target);
        Message = sent ? "Alteração salva." : "Alteração guardada neste navegador; aguardando sincronização.";
        if (sent) await LoadAsync();
        return sent;
    }
    protected async Task DeleteAsync(string url, string description, FinancialRecord record)
    {
        if (!await Js.InvokeAsync<bool>("confirm", $"Excluir {description}? Esta ação também remove os pagamentos quando for uma despesa.")) return;
        await RunAsync(async () => { var sent = await Api.SendAsync(HttpMethod.Delete, url, new { }, record); Message = sent ? "Registro excluído." : "Exclusão aguardando sincronização."; if (sent) await LoadAsync(); });
    }
    private async Task RefreshAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stop.Token))
                await InvokeAsync(async () => { if (!Busy && await Js.InvokeAsync<bool>("financeQueue.active")) { try { await Api.SynchronizeAsync(); await ReloadAsync(); } catch (UnauthorizedAccessException) { Navigation.NavigateTo("/login"); } catch (Exception e) { Error = e.Message; } StateHasChanged(); } });
        }
        catch (OperationCanceledException) { }
    }
    public async ValueTask DisposeAsync() { stop.Cancel(); if (refresh is not null) await refresh; if(reference is not null){try{await Js.InvokeVoidAsync("financeQueue.unwatch");}catch(JSDisconnectedException){} reference.Dispose();} stop.Dispose(); }
}
