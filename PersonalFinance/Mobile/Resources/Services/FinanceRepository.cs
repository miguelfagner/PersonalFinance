using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PersonalFinance.Contracts;
using PersonalFinance.Resources.Models;

namespace PersonalFinance.Resources.Services;

// Keeps the Android screens' local IDs while the server is the preferred writer.
// DatabaseService remains the offline store and the stable ID map.
public sealed class FinanceRepository : DatabaseService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { TypeInfoResolver = FinanceJsonContext.Default };

    private static async Task RefreshAsync()
    {
        if (AndroidSync.Connected && TokenVault.Read() is not null) await AndroidSync.SyncAsync();
    }

    private async Task<T> StampAsync<T>(T row)
    {
        if (row is null) return row;
        switch (row)
        {
            case Receita x: x.LoadedRevision = await KnownRevisionAsync(nameof(Income), x.ClientId); break;
            case Despesa x: x.LoadedRevision = await KnownRevisionAsync(nameof(Expense), x.ClientId); break;
            case Transacao x: x.LoadedRevision = await KnownRevisionAsync(nameof(Payment), x.ClientId); break;
            case ReceitaFixa x: x.LoadedRevision = await KnownRevisionAsync(nameof(FixedIncome), x.ClientId); break;
            case DespesaFixa x: x.LoadedRevision = await KnownRevisionAsync(nameof(FixedExpense), x.ClientId); break;
        }
        return row;
    }
    private async Task<List<T>> StampAsync<T>(List<T> rows)
    {
        foreach (var row in rows) await StampAsync(row);
        return rows;
    }

    new internal async Task<List<Receita>> ListaReceitasAsync(DateTime start, DateTime end) { await RefreshAsync(); return await StampAsync(await base.ListaReceitasAsync(start,end)); }
    new internal async Task<List<Receita>> ListaReceitasAsync() { await RefreshAsync(); return await StampAsync(await base.ListaReceitasAsync()); }
    new internal async Task<Receita> PegarReceitaAsync(int id) { await RefreshAsync(); return await StampAsync(await base.PegarReceitaAsync(id)); }
    new internal async Task<List<Despesa>> ListaDespesasAsync(DateTime start, DateTime end) { await RefreshAsync(); return await StampAsync(await base.ListaDespesasAsync(start,end)); }
    new internal async Task<List<Despesa>> ListaDespesasAsync() { await RefreshAsync(); return await StampAsync(await base.ListaDespesasAsync()); }
    new internal async Task<Despesa> PegarDespesaAsync(int id) { await RefreshAsync(); return await StampAsync(await base.PegarDespesaAsync(id)); }
    new internal async Task<List<DespesaEmAberto>> ListaDespesasEmAbertoAsync(DateTime start, DateTime end, DateTime? cutoff = null) { await RefreshAsync(); return await base.ListaDespesasEmAbertoAsync(start,end,cutoff); }
    new internal async Task<List<Transacao>> ListaTransacoesAsync(DateTime start, DateTime end) { await RefreshAsync(); return await StampAsync(await base.ListaTransacoesAsync(start,end)); }
    new internal async Task<List<Transacao>> ListaTransacoesAsync(int id) { await RefreshAsync(); return await StampAsync(await base.ListaTransacoesAsync(id)); }
    new internal async Task<Transacao> PegarTransacaoAsync(int id) { await RefreshAsync(); return await StampAsync(await base.PegarTransacaoAsync(id)); }
    new public async Task<List<ReceitaFixa>> ListarReceitasFixasAsync(bool incluirInativas = false) { await RefreshAsync(); return await StampAsync(await base.ListarReceitasFixasAsync(incluirInativas)); }
    new public async Task<ReceitaFixa?> PegarReceitaFixaAsync(int id) { await RefreshAsync(); return await StampAsync(await base.PegarReceitaFixaAsync(id)); }
    new public async Task<List<DespesaFixa>> ListarDespesasFixasAsync(bool incluirInativas = false) { await RefreshAsync(); return await StampAsync(await base.ListarDespesasFixasAsync(incluirInativas)); }
    new public async Task<DespesaFixa?> PegarDespesaFixaAsync(int id) { await RefreshAsync(); return await StampAsync(await base.PegarDespesaFixaAsync(id)); }
    new public async Task<List<Despesa>> ListaDespesasPessoaisAsync(DateTime month) { await RefreshAsync(); return await base.ListaDespesasPessoaisAsync(month); }
    new public async Task<ResumoPessoal> ObterResumoPessoalAsync(DateTime month) { await RefreshAsync(); return await base.ObterResumoPessoalAsync(month); }
    new public async Task<List<PreparacaoMensalItem>> PreverPreparacaoMensalAsync(DateTime month) { await RefreshAsync(); return await base.PreverPreparacaoMensalAsync(month); }

    private async Task<Guid?> IdentityAsync(string table, int? id)
    {
        if (!id.HasValue || id.Value == 0) return null;
        return table switch
        {
            "Receita" => (await base.PegarReceitaAsync(id.Value))?.ClientId,
            "Despesa" => (await base.PegarDespesaAsync(id.Value))?.ClientId,
            "ReceitaFixa" => (await base.PegarReceitaFixaAsync(id.Value))?.ClientId,
            "DespesaFixa" => (await base.PegarDespesaFixaAsync(id.Value))?.ClientId,
            _ => null
        };
    }

    private async Task<int> SubmitAsync(FinanceCommand command, string table, Func<Task<int>> local,
        Action<FinanceCommandResult>? onConfirmed = null)
    {
        var state=await GetSyncStateAsync();
        if (!state.Initialized && (await SnapshotAsync()).Count == 0)
            throw new InvalidOperationException("Conecte a mesma conta do Blazor para preparar os dados antes do primeiro lançamento.");
        if (!state.Initialized || state.NeedsReconciliation || TokenVault.Read() is null)
            return await local();
        await AndroidSync.SyncAsync();
        await AndroidSync.Gate.WaitAsync();
        var confirmed=false;
        try
        {
            var priorCommands = await PendingCommandsAsync(AndroidSync.Url, AndroidSync.Email);
            var priorRows = await PendingSyncCountAsync();
            if (priorRows > 0 && priorCommands.Count == 0) return await local();
            var predecessor = priorCommands.LastOrDefault(x =>
            {
                var earlier = JsonSerializer.Deserialize<FinanceCommand>(x.Payload, Json);
                return earlier?.EntityType == command.EntityType && earlier.ClientId == command.ClientId;
            });
            if (predecessor is not null) command = command with { DependsOn = predecessor.OperationId };
            var pending=new SyncCommandPending { OperationId=command.OperationId, Server=AndroidSync.Url,
                Email=AndroidSync.Email, Payload=JsonSerializer.Serialize(command,Json), CreatedAtUtc=DateTime.UtcNow };
            // Write ahead of the HTTP request: a timeout after server commit keeps the same identity for retry.
            await QueueCommandAsync(pending);
            if (!AndroidSync.Connected || priorCommands.Count>0)
                return await ApplyOfflineAsync(command.OperationId, local);
            try
            {
                using var http=await AndroidSync.ClientAsync();
                http.Timeout=TimeSpan.FromSeconds(12);
                using var response=await http.PostAsJsonAsync("api/commands",command,Json);
                if (response.StatusCode==HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException("Entre novamente para continuar.");
                FinanceCommandResult? result=null;
                try { result=await response.Content.ReadFromJsonAsync<FinanceCommandResult>(Json); } catch(JsonException) { }
                if (response.StatusCode==HttpStatusCode.Conflict && result?.Status=="Conflict")
                {
                    await ArchiveCommandConflictAsync(pending,result.Error??"O servidor possui uma versão mais recente.",result.Current);
                    throw new InvalidOperationException(result.Error);
                }
                if (!response.IsSuccessStatusCode || result?.Status!="Applied")
                    throw new InvalidOperationException(result?.Error??$"O servidor não confirmou a alteração (HTTP {(int)response.StatusCode}).");
                await ApplyConfirmedCommandAsync(command,result);
                await AcceptCommandAndRebaseAsync(command.OperationId,result.Revisions);
                onConfirmed?.Invoke(result);
                confirmed=true;
            }
            catch (HttpRequestException) { return await ApplyOfflineAsync(command.OperationId, local); }
            catch (TaskCanceledException) { return await ApplyOfflineAsync(command.OperationId, local); }
        }
        finally { AndroidSync.Gate.Release(); }
        if (confirmed)
        {
            await AndroidSync.SyncAsync();
            return command.Kind=="Delete" || string.IsNullOrEmpty(table)?1:await LocalIdAsync(table,command.ClientId);
        }
        throw new InvalidOperationException("A alteração não foi concluída.");
    }

    private async Task<int> ApplyOfflineAsync(Guid operation, Func<Task<int>> local)
    {
        try { return await RunLocallyWithoutSyncAsync(local); }
        catch { await AcceptCommandAsync(operation); throw; }
    }

    private async Task<FinanceCommand> UpsertAsync(string type, Guid id, int localId, object data,
        long? loadedRevision, Guid? parent=null, Guid? fixedId=null)
    {
        var revision=localId==0?0:loadedRevision ?? await KnownRevisionAsync(type,id);
        return new(Guid.NewGuid(),"Upsert",type,id,revision,JsonSerializer.SerializeToElement(data,data.GetType(),Json),parent,fixedId);
    }

    new internal async Task<int> SalvarReceitaAsync(Receita item)
    {
        var dto=new Income { ClientId=item.ClientId, ReferenceMonth=DateOnly.FromDateTime(item.MesReferencia),
            Payer=item.FontePagadora??"",Description=item.Descricao??"",Type=item.Tipo??"",Amount=item.Valor,
            GeneratedForMonth=item.CompetenciaGeracao.HasValue?DateOnly.FromDateTime(item.CompetenciaGeracao.Value):null };
        var command=await UpsertAsync(nameof(Income),item.ClientId,item.Id,dto,item.LoadedRevision,fixedId:await IdentityAsync("ReceitaFixa",item.ReceitaFixaId));
        var id=await SubmitAsync(command,"Receita",()=>base.SalvarReceitaAsync(item));if(id>0)item.Id=id;return id;
    }
    new internal async Task<int> SalvarDespesaAsync(Despesa item)
    {
        var dto=new Expense { ClientId=item.ClientId, CreatedOn=DateOnly.FromDateTime(item.DataCadastro),
            DueDate=DateOnly.FromDateTime(item.Vencimento),InstallmentNumber=item.NParcela,Description=item.Descricao??"",
            Category=item.Categoria??"",Amount=item.Valor,Replicable=item.replicavel,
            GeneratedForMonth=item.CompetenciaGeracao.HasValue?DateOnly.FromDateTime(item.CompetenciaGeracao.Value):null };
        var command=await UpsertAsync(nameof(Expense),item.ClientId,item.Id,dto,item.LoadedRevision,
            await IdentityAsync("Receita",item.ReceitaId),await IdentityAsync("DespesaFixa",item.DespesaFixaId));
        var id=await SubmitAsync(command,"Despesa",()=>base.SalvarDespesaAsync(item));if(id>0)item.Id=id;return id;
    }
    new internal async Task<int> SalvarTransacaoAsync(Transacao item)
    {
        var dto=new Payment { ClientId=item.ClientId,Date=DateOnly.FromDateTime(item.Data),Note=item.Observacao??"",Amount=item.Valor };
        var command=await UpsertAsync(nameof(Payment),item.ClientId,item.Id,dto,item.LoadedRevision,await IdentityAsync("Despesa",item.DespesaId));
        var id=await SubmitAsync(command,"Transacao",()=>base.SalvarTransacaoAsync(item));if(id>0)item.Id=id;return id;
    }
    new public async Task<int> SalvarReceitaFixaAsync(ReceitaFixa item)
    {
        var dto=new FixedIncome { ClientId=item.ClientId,Payer=item.FontePagadora,Description=item.Descricao,
            Type=item.Tipo,Amount=item.Valor,Active=item.Ativa };
        var command=await UpsertAsync(nameof(FixedIncome),item.ClientId,item.Id,dto,item.LoadedRevision);
        var id=await SubmitAsync(command,"ReceitaFixa",()=>base.SalvarReceitaFixaAsync(item));if(id>0)item.Id=id;return id;
    }
    new public async Task<int> SalvarDespesaFixaAsync(DespesaFixa item)
    {
        var dto=new FixedExpense { ClientId=item.ClientId,Description=item.Descricao,Category=item.Categoria,
            Amount=item.Valor,DueDay=item.DiaVencimento,Active=item.Ativa };
        var command=await UpsertAsync(nameof(FixedExpense),item.ClientId,item.Id,dto,item.LoadedRevision,await IdentityAsync("ReceitaFixa",item.ReceitaFixaId));
        var id=await SubmitAsync(command,"DespesaFixa",()=>base.SalvarDespesaFixaAsync(item));if(id>0)item.Id=id;return id;
    }
    private async Task<int> DeleteAsync(string type, string table, Guid id, long? loadedRevision, Func<Task<int>> local)
    {
        var command=new FinanceCommand(Guid.NewGuid(),"Delete",type,id,loadedRevision ?? await KnownRevisionAsync(type,id),null);
        return await SubmitAsync(command,table,local);
    }
    new public Task<int> DeletarReceitaAsync(Receita item) => DeleteAsync(nameof(Income),"Receita",item.ClientId,item.LoadedRevision,()=>base.DeletarReceitaAsync(item));
    new public Task<int> DeletarDespesaAsync(Despesa item) => DeleteAsync(nameof(Expense),"Despesa",item.ClientId,item.LoadedRevision,()=>base.DeletarDespesaAsync(item));
    new public Task<int> DeletarTransacaoAsync(Transacao item) => DeleteAsync(nameof(Payment),"Transacao",item.ClientId,item.LoadedRevision,()=>base.DeletarTransacaoAsync(item));
    new public Task<int> ExcluirReceitaFixaAsync(int id) => DeleteFixedIncomeAsync(id);
    private async Task<int> DeleteFixedIncomeAsync(int id)
    { var item=await PegarReceitaFixaAsync(id)??throw new InvalidOperationException("Receita fixa não encontrada.");return await DeleteAsync(nameof(FixedIncome),"ReceitaFixa",item.ClientId,item.LoadedRevision,()=>base.ExcluirReceitaFixaAsync(id)); }
    new public Task<int> ExcluirDespesaFixaAsync(int id) => DeleteFixedExpenseAsync(id);
    private async Task<int> DeleteFixedExpenseAsync(int id)
    { var item=await PegarDespesaFixaAsync(id)??throw new InvalidOperationException("Despesa fixa não encontrada.");return await DeleteAsync(nameof(FixedExpense),"DespesaFixa",item.ClientId,item.LoadedRevision,()=>base.ExcluirDespesaFixaAsync(id)); }
    new public async Task<int> PausarReceitaFixaAsync(int id,bool pausada=true)
    { var item=await PegarReceitaFixaAsync(id)??throw new InvalidOperationException("Receita fixa não encontrada.");item.Ativa=!pausada;return await SalvarReceitaFixaAsync(item); }
    new public async Task<int> PausarDespesaFixaAsync(int id,bool pausada=true)
    { var item=await PegarDespesaFixaAsync(id)??throw new InvalidOperationException("Despesa fixa não encontrada.");item.Ativa=!pausada;return await SalvarDespesaFixaAsync(item); }
    new public async Task<int> SalvarGastoPessoalAsync(Transacao item)
    {
        var budget = await IdentityAsync("Despesa", item.DespesaId)
            ?? throw new InvalidOperationException("O orçamento pessoal selecionado não existe mais.");
        var request = new PersonalExpenseRequest(item.DespesaId, DateOnly.FromDateTime(item.Data),
            item.Observacao ?? "", item.Valor, item.ClientId);
        var command = new FinanceCommand(Guid.NewGuid(), "Personal", nameof(Payment), item.ClientId,
            0, JsonSerializer.SerializeToElement(request, Json), budget);
        var id = await SubmitAsync(command, "Transacao", () => base.SalvarGastoPessoalAsync(item));
        if (id > 0) item.Id = id;
        return id;
    }
    new public async Task<PreparacaoMensalResultado> PrepararMesAsync(DateTime month,
        IReadOnlyCollection<PreparacaoMensalItem> items)
    {
        var selected = new List<PrepareIdentityItem>();
        foreach (var item in items)
        {
            var type = item.TipoModelo == TiposModeloFixo.Receita ? "INCOME" : "EXPENSE";
            var modelId = await IdentityAsync(type == "INCOME" ? "ReceitaFixa" : "DespesaFixa", item.ModeloId)
                ?? throw new InvalidOperationException("Um modelo mensal foi removido. Atualize a preparação.");
            selected.Add(new(type, modelId, item.Selecionado, item.FontePagadora, item.Descricao,
                item.Tipo, item.Categoria, item.Valor, item.DiaVencimento));
        }
        var request = new PrepareIdentityRequest(DateOnly.FromDateTime(month), selected);
        var command = new FinanceCommand(Guid.NewGuid(), "PrepareIdentity", "", Guid.Empty, null,
            JsonSerializer.SerializeToElement(request, Json));
        PreparacaoMensalResultado? outcome = null;
        await SubmitAsync(command, "", async () =>
        {
            outcome = await base.PrepararMesAsync(month, items);
            return 1;
        }, result =>
        {
            var details = result.Data?.Deserialize<PreparationResult>(Json);
            if (details is null) return;
            outcome = new() { Competencia = month, ReceitasCriadas = details.IncomesCreated,
                ReceitasExistentes = details.IncomesExisting, DespesasCriadas = details.ExpensesCreated,
                DespesasExistentes = details.ExpensesExisting, Ignorados = details.Ignored };
        });
        return outcome ?? new PreparacaoMensalResultado { Competencia = month };
    }
    new public async Task<ImportacaoFixosResultado> ImportarLancamentosComoFixosAsync(DateTime month,
        IReadOnlyCollection<int> incomeIds, IReadOnlyCollection<int> expenseIds)
    {
        var incomes = new List<Guid>();
        foreach (var id in incomeIds)
            incomes.Add(await IdentityAsync("Receita", id) ?? throw new InvalidOperationException("Receita selecionada não encontrada."));
        var expenses = new List<Guid>();
        foreach (var id in expenseIds)
            expenses.Add(await IdentityAsync("Despesa", id) ?? throw new InvalidOperationException("Despesa selecionada não encontrada."));
        var request = new ConvertIdentityRequest(DateOnly.FromDateTime(month), incomes, expenses);
        var command = new FinanceCommand(Guid.NewGuid(), "ConvertIdentity", "", Guid.Empty, null,
            JsonSerializer.SerializeToElement(request, Json));
        ImportacaoFixosResultado? outcome = null;
        await SubmitAsync(command, "", async () =>
        {
            outcome = await base.ImportarLancamentosComoFixosAsync(month, incomeIds, expenseIds);
            return 1;
        }, result =>
        {
            var details = result.Data?.Deserialize<ConvertFixedResult>(Json);
            if (details is null) return;
            outcome = new() { ReceitasCriadas = details.IncomesCreated,
                ReceitasReutilizadas = details.IncomesReused, DespesasCriadas = details.ExpensesCreated,
                DespesasReutilizadas = details.ExpensesReused };
        });
        return outcome ?? new ImportacaoFixosResultado();
    }
    new public Task<int> AtualizaStatusAsync(int id) => RunLocallyWithoutSyncAsync(()=>base.AtualizaStatusAsync(id));
}
