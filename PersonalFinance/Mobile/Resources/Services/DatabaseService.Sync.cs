using PersonalFinance.Contracts;
using PersonalFinance.Resources.Models;
using SQLite;
using System.Text.Json;

namespace PersonalFinance.Resources.Services;

public partial class DatabaseService
{
    private static readonly JsonSerializerOptions SyncJson = new(JsonSerializerDefaults.Web) { TypeInfoResolver = FinanceJsonContext.Default };
    private static readonly SemaphoreSlim StartupGate = new(1, 1);
    private static readonly HashSet<string> RecoveredDatabases = [];
    private const string SqlUuid = "(lower(hex(randomblob(4)))||'-'||lower(hex(randomblob(2)))||'-'||lower(hex(randomblob(2)))||'-'||lower(hex(randomblob(2)))||'-'||lower(hex(randomblob(6))))";
    private static readonly (string Table, string Type)[] SyncTables = [("ReceitaFixa", "FixedIncome"), ("DespesaFixa", "FixedExpense"), ("Receita", "Income"), ("Despesa", "Expense"), ("Transacao", "Payment"), ("PreparacaoMensalItem", "MonthlyPreparationItem")];
    private async Task InitializeSyncAsync()
    {
        // The constructor waits synchronously; initialization must never resume on the UI context.
        await StartupGate.WaitAsync().ConfigureAwait(false);
        try
        {
        var path = Android.App.Application.Context.FilesDir!.AbsolutePath;
        if (RecoveredDatabases.Contains(path)) return;
        await _db.CreateTableAsync<SyncPending>().ConfigureAwait(false);
        await _db.CreateTableAsync<SyncControl>().ConfigureAwait(false);
        await _db.CreateTableAsync<SyncDeleted>().ConfigureAwait(false);
        await _db.CreateTableAsync<SyncEntityRevision>().ConfigureAwait(false);
        await _db.CreateTableAsync<SyncRejected>().ConfigureAwait(false);
        await _db.CreateTableAsync<SyncCommandPending>().ConfigureAwait(false);
        await _db.RunInTransactionAsync(c =>
        {
            var first = c.Find<SyncControl>(1) is null;
            if (first) c.Insert(new SyncControl { Suppressed = true });
            else c.Execute("UPDATE SyncControl SET Suppressed=0 WHERE Id=1");
            foreach (var (table, type) in SyncTables)
            {
                c.Execute($"UPDATE {table} SET ClientId={SqlUuid} WHERE ClientId IS NULL OR ClientId='' OR ClientId='00000000-0000-0000-0000-000000000000'");
                foreach (var action in new[] { "INSERT", "UPDATE", "DELETE" })
                {
                    var row = action == "DELETE" ? "OLD" : "NEW";
                    c.Execute($"DROP TRIGGER IF EXISTS sync_{table}_{action}");
                    c.Execute($"CREATE TRIGGER sync_{table}_{action} AFTER {action} ON {table} WHEN (SELECT Suppressed FROM SyncControl WHERE Id=1)=0 BEGIN INSERT INTO SyncPending (OperationId,EntityType,ClientId,Deleted,BaseRevision) VALUES({SqlUuid},'{type}',{row}.ClientId,{(action == "DELETE" ? 1 : 0)},(SELECT Revision FROM SyncEntityRevision WHERE Key='{type}:'||{row}.ClientId)); END");
                }
                c.Execute($"CREATE TRIGGER IF NOT EXISTS tombstone_{table} AFTER DELETE ON {table} WHEN (SELECT Suppressed FROM SyncControl WHERE Id=1)=0 BEGIN INSERT OR IGNORE INTO SyncDeleted (Key) VALUES('{type}:'||OLD.ClientId); END");
                if (first) c.Execute($"INSERT INTO SyncPending(OperationId,EntityType,ClientId,Deleted,BaseRevision) SELECT {SqlUuid},'{type}',ClientId,0,0 FROM {table}");
            }
            if (first) c.Execute("UPDATE SyncControl SET Suppressed=0 WHERE Id=1");
            else if (c.Find<SyncControl>(1)?.Initialized == true && c.Table<SyncPending>().Any(x => x.BaseRevision == null))
                c.Execute("UPDATE SyncControl SET NeedsReconciliation=1 WHERE Id=1");
        }).ConfigureAwait(false);
        RecoveredDatabases.Add(path);
        }
        finally { StartupGate.Release(); }
    }
    internal static async Task InvalidateSyncInitializationAsync()
    {
        await StartupGate.WaitAsync();
        try { RecoveredDatabases.Remove(Android.App.Application.Context.FilesDir!.AbsolutePath); }
        finally { StartupGate.Release(); }
    }
    public Task<SyncControl> GetSyncStateAsync() => _db.FindAsync<SyncControl>(1);
    public Task<int> PendingSyncCountAsync() => _db.Table<SyncPending>().CountAsync();
    public Task<int> PendingCommandCountAsync() => _db.Table<SyncCommandPending>().CountAsync();
    public Task<List<SyncCommandPending>> PendingCommandsAsync(string server, string email) =>
        _db.Table<SyncCommandPending>().Where(x => x.Server == server && x.Email == email).OrderBy(x => x.CreatedAtUtc).ToListAsync();
    public Task QueueCommandAsync(SyncCommandPending command) => _db.InsertAsync(command);
    public Task AcceptCommandAsync(Guid operation) => _db.ExecuteAsync("DELETE FROM SyncCommandPending WHERE OperationId=?", operation);
    public Task ApplyConfirmedCommandAsync(FinanceCommand command, FinanceCommandResult result) => _db.RunInTransactionAsync(c =>
    {
        if (command.Kind is not ("Upsert" or "Personal" or "Delete")) return;
        var data = result.Data;
        if (command.Kind != "Delete" && data is null) return;
        long? revision = data.HasValue && data.Value.TryGetProperty("revision", out var value) && value.TryGetInt64(out var number) ? number : null;
        var change = new SyncChange(Guid.Empty, command.EntityType, command.ClientId, command.Kind == "Delete", data,
            command.ParentClientId, command.FixedClientId) { Revision = revision };
        c.Execute("UPDATE SyncControl SET Suppressed=1 WHERE Id=1");
        ApplyChange(c, change);
        RecordRevision(c, change);
        RecalculateStatuses(c);
        c.Execute("UPDATE SyncControl SET Suppressed=0 WHERE Id=1");
    });
    public Task AcceptCommandAndRebaseAsync(Guid operation, IReadOnlyDictionary<Guid,long> revisions) => _db.RunInTransactionAsync(c =>
    {
        c.Execute("DELETE FROM SyncCommandPending WHERE OperationId=?", operation);
        foreach (var next in c.Table<SyncCommandPending>().ToList())
        {
            var command = JsonSerializer.Deserialize<FinanceCommand>(next.Payload, SyncJson);
            if (command is null || !revisions.TryGetValue(command.ClientId, out var revision)) continue;
            next.Payload = JsonSerializer.Serialize(command with { ExpectedRevision = revision }, SyncJson);
            c.Update(next);
        }
    });
    public Task<long?> KnownRevisionAsync(string type, Guid id) => _db.ExecuteScalarAsync<long?>("SELECT Revision FROM SyncEntityRevision WHERE Key=?", type + ":" + id);
    public Task<int> LocalIdAsync(string table, Guid id) => _db.ExecuteScalarAsync<int>($"SELECT Id FROM {table} WHERE ClientId=?", id);
    public async Task<int> RunLocallyWithoutSyncAsync(Func<Task<int>> action)
    {
        await _db.ExecuteAsync("UPDATE SyncControl SET Suppressed=1 WHERE Id=1");
        try { return await action(); }
        finally { await _db.ExecuteAsync("UPDATE SyncControl SET Suppressed=0 WHERE Id=1"); }
    }
    public Task<int> RejectedSyncCountAsync() => _db.Table<SyncRejected>().CountAsync();
    public Task<List<SyncRejected>> RejectedSyncAsync() => _db.Table<SyncRejected>().OrderByDescending(x => x.RejectedAtUtc).ToListAsync();
    public Task MarkReconciliationAsync() => _db.ExecuteAsync("UPDATE SyncControl SET NeedsReconciliation=1 WHERE Id=1");
    public Task CreateConsistentBackupAsync(string path) => _db.BackupAsync(path);
    private static Guid SnapshotIdentity(SQLiteConnection c,string type,Guid model,DateTime month)
    {
        var id=FinanceIdentity.ForMonth(type,model,DateOnly.FromDateTime(month));
        return c.Find<SyncDeleted>(type+":"+id.ToString()) is null ? id : Guid.NewGuid();
    }
    public Task DiscardPendingAsync(Guid operation) => _db.RunInTransactionAsync(c => { c.Execute("DELETE FROM SyncPending WHERE OperationId=?",operation);c.Execute("UPDATE SyncControl SET Cursor=0 WHERE Id=1"); });
    public Task RemapIdentitiesAsync(IReadOnlyDictionary<Guid,Guid> mapping) => _db.RunInTransactionAsync(c =>
    {
        c.Execute("UPDATE SyncControl SET Suppressed=1,Cursor=0 WHERE Id=1");
        foreach(var (from,to) in mapping)
        {
            foreach(var (table,_) in SyncTables)c.Execute($"UPDATE {table} SET ClientId=? WHERE ClientId=?",to,from);
            c.Execute("UPDATE SyncPending SET ClientId=? WHERE ClientId=?",to,from);
        }
        c.Execute("UPDATE SyncControl SET Suppressed=0 WHERE Id=1");
    });

    public async Task<List<SyncPending>> FreezePendingAsync(bool all = false)
    {
        var result = new List<SyncPending>();
        await _db.RunInTransactionAsync(c =>
        {
            foreach (var group in c.Table<SyncPending>().Where(x => x.Payload == null).ToList().GroupBy(x => (x.EntityType, x.ClientId)))
            {
                var latest = group.OrderByDescending(x => x.Id).First();
                latest.BaseRevision = group.OrderBy(x => x.Id).First().BaseRevision;
                latest.Payload = JsonSerializer.Serialize(MakeChange(c, latest), SyncJson); c.Update(latest);
                foreach (var old in group.Where(x => x.Id != latest.Id)) c.Delete(old);
            }
            result = c.Table<SyncPending>().ToList().OrderBy(x => x.Deleted ? 10 - Order(x.EntityType) : Order(x.EntityType)).ThenBy(x => x.Id).Take(all ? int.MaxValue : 500).ToList();
        });
        return result;
    }
    private static int Order(string type) => Array.FindIndex(SyncTables, x => x.Type == type);
    private static Guid? Identity<T>(SQLiteConnection c, int? id) where T : new()
    {
        if (!id.HasValue) return null;
        var row = c.Find<T>(id.Value);
        return row is null ? null : (Guid?)typeof(T).GetProperty("ClientId")!.GetValue(row);
    }
    private static SyncChange MakeChange(SQLiteConnection c, SyncPending pending)
    {
        object? dto = null; Guid? parent = null; Guid? fixedId = null;
        if (!pending.Deleted)
        {
            switch (pending.EntityType)
            {
                case "FixedIncome":
                    var fi = c.Table<ReceitaFixa>().FirstOrDefault(x => x.ClientId == pending.ClientId);
                    if (fi is not null) dto = new FixedIncome { ClientId=fi.ClientId, Payer=fi.FontePagadora, Description=fi.Descricao, Type=fi.Tipo, Amount=fi.Valor, Active=fi.Ativa };
                    break;
                case "FixedExpense":
                    var fe = c.Table<DespesaFixa>().FirstOrDefault(x => x.ClientId == pending.ClientId);
                    if (fe is not null) { dto = new FixedExpense { ClientId=fe.ClientId, Description=fe.Descricao, Category=fe.Categoria, Amount=fe.Valor, DueDay=fe.DiaVencimento, Active=fe.Ativa }; parent=Identity<ReceitaFixa>(c,fe.ReceitaFixaId); }
                    break;
                case "Income":
                    var i = c.Table<Receita>().FirstOrDefault(x => x.ClientId == pending.ClientId);
                    if (i is not null) { dto = new Income { ClientId=i.ClientId, ReferenceMonth=DateOnly.FromDateTime(i.MesReferencia), Payer=i.FontePagadora, Description=i.Descricao, Type=i.Tipo, Amount=i.Valor, GeneratedForMonth=Date(i.CompetenciaGeracao) }; fixedId=Identity<ReceitaFixa>(c,i.ReceitaFixaId); }
                    break;
                case "Expense":
                    var e = c.Table<Despesa>().FirstOrDefault(x => x.ClientId == pending.ClientId);
                    if (e is not null) { dto = new Expense { ClientId=e.ClientId, CreatedOn=DateOnly.FromDateTime(e.DataCadastro), DueDate=DateOnly.FromDateTime(e.Vencimento), InstallmentNumber=e.NParcela, Description=e.Descricao, Category=e.Categoria, Amount=e.Valor, Replicable=e.replicavel, GeneratedForMonth=Date(e.CompetenciaGeracao) }; parent=Identity<Receita>(c,e.ReceitaId); fixedId=Identity<DespesaFixa>(c,e.DespesaFixaId); }
                    break;
                case "Payment":
                    var p = c.Table<Transacao>().FirstOrDefault(x => x.ClientId == pending.ClientId);
                    if (p is not null) { dto = new Payment { ClientId=p.ClientId, Date=DateOnly.FromDateTime(p.Data), Note=p.Observacao, Amount=p.Valor }; parent=Identity<Despesa>(c,p.DespesaId); }
                    break;
                case "MonthlyPreparationItem":
                    var m = c.Table<PreparacaoMensalItem>().FirstOrDefault(x => x.ClientId == pending.ClientId);
                    if (m is not null) { dto = new MonthlyPreparationItem { ClientId=m.ClientId, ModelType=m.TipoModelo=="RECEITA"?"INCOME":"EXPENSE", ReferenceMonth=DateOnly.FromDateTime(m.Competencia), State=m.Estado=="GERADO"?"GENERATED":"IGNORED" }; parent=m.TipoModelo=="RECEITA"?Identity<ReceitaFixa>(c,m.ModeloId):Identity<DespesaFixa>(c,m.ModeloId); }
                    break;
            }
        }
        return new SyncChange(pending.OperationId,pending.EntityType,pending.ClientId,pending.Deleted||dto is null,dto is null?null:JsonSerializer.SerializeToElement(dto,dto.GetType(),SyncJson),parent,fixedId)
        { ExpectedRevision = pending.BaseRevision };
    }
    public async Task<List<SyncChange>> SnapshotAsync()
    {
        var result = new List<SyncChange>();
        await _db.RunInTransactionAsync(c =>
        {
            foreach(var (table,type) in SyncTables)
                foreach(var row in c.Query<SyncPending>($"SELECT ClientId FROM {table}"))
                { row.EntityType=type;row.OperationId=Guid.NewGuid();result.Add(MakeChange(c,row)); }
        });
        return result;
    }
    public async Task<DeviceSnapshotCheckpoint> CaptureDeviceSnapshotAsync(long cursor, string server, string email)
    {
        DeviceSnapshotCheckpoint checkpoint = null!;
        await _db.RunInTransactionAsync(c =>
        {
            var changes = new List<SyncChange>();
            foreach (var (table, type) in SyncTables)
                foreach (var row in c.Query<SyncPending>($"SELECT ClientId FROM {table}"))
                { row.EntityType = type; row.OperationId = Guid.NewGuid(); changes.Add(MakeChange(c, row)); }
            checkpoint = new(new(Guid.NewGuid(), changes, cursor), server, email,
                c.Table<SyncPending>().ToList().Select(x => x.OperationId).ToList(),
                c.Table<SyncCommandPending>().Where(x => x.Server == server && x.Email == email).ToList().Select(x => x.OperationId).ToList());
        });
        return checkpoint;
    }
    public Task CompleteDeviceSnapshotAsync(DeviceSnapshotCheckpoint checkpoint, SyncPullResult result) => _db.RunInTransactionAsync(c =>
    {
        foreach (var id in checkpoint.PendingIds) c.Execute("DELETE FROM SyncPending WHERE OperationId=?", id);
        foreach (var id in checkpoint.CommandIds) c.Execute("DELETE FROM SyncCommandPending WHERE OperationId=?", id);
        foreach (var change in result.Changes)
        {
            RecordRevision(c, change);
            foreach (var pending in c.Table<SyncPending>().Where(x => x.EntityType == change.EntityType && x.ClientId == change.ClientId).ToList())
            { pending.BaseRevision = change.Revision; pending.Payload = null; c.Update(pending); }
        }
        var revisions = result.Changes.ToDictionary(x => (x.EntityType, x.ClientId), x => x.Revision ?? 0);
        foreach (var pending in c.Table<SyncCommandPending>().Where(x => x.Server == checkpoint.Server && x.Email == checkpoint.Email).ToList())
        {
            var command = JsonSerializer.Deserialize<FinanceCommand>(pending.Payload, SyncJson)!;
            if (revisions.TryGetValue((command.EntityType, command.ClientId), out var revision))
                command = command with { ExpectedRevision = revision };
            if (command.DependsOn.HasValue && checkpoint.CommandIds.Contains(command.DependsOn.Value))
                command = command with { DependsOn = null };
            pending.Payload = JsonSerializer.Serialize(command, SyncJson); c.Update(pending);
        }
        c.Execute("UPDATE SyncControl SET Initialized=1,NeedsReconciliation=0,Cursor=?,LastSyncUtc=? WHERE Id=1", result.Cursor, DateTime.UtcNow);
    });
    public Task AcceptPendingAsync(IEnumerable<Guid> ids) => _db.RunInTransactionAsync(c => { foreach(var id in ids)c.Execute("DELETE FROM SyncPending WHERE OperationId=?",id); });
    public Task AcceptPendingAndRebaseAsync(SyncPushResult result) => _db.RunInTransactionAsync(c =>
    {
        foreach (var operation in result.Accepted)
        {
            var accepted = c.Table<SyncPending>().FirstOrDefault(x => x.OperationId == operation);
            if (accepted is null) continue;
            c.Delete(accepted);
            if (!result.Revisions.TryGetValue(operation, out var revision)) continue;
            c.InsertOrReplace(new SyncEntityRevision { Key = accepted.EntityType + ":" + accepted.ClientId, Revision = revision });
            foreach (var later in c.Table<SyncPending>().Where(x => x.EntityType == accepted.EntityType && x.ClientId == accepted.ClientId).ToList())
            {
                later.BaseRevision = revision;
                if (later.Payload is not null)
                {
                    var change = JsonSerializer.Deserialize<SyncChange>(later.Payload, SyncJson)!;
                    later.Payload = JsonSerializer.Serialize(change with { ExpectedRevision = revision }, SyncJson);
                }
                c.Update(later);
            }
        }
    });
    public Task CompleteBootstrapAsync(IEnumerable<Guid> accepted) => _db.RunInTransactionAsync(c => {foreach(var id in accepted)c.Execute("DELETE FROM SyncPending WHERE OperationId=?",id);c.Execute("UPDATE SyncControl SET Initialized=1,NeedsReconciliation=0 WHERE Id=1");});

    public async Task ApplyPullAsync(SyncPullResult page)
    {
        await _db.RunInTransactionAsync(c =>
        {
            c.Execute("UPDATE SyncControl SET Suppressed=1 WHERE Id=1");
            foreach(var change in page.Changes)
            {
                RecordRevision(c,change);
                if(c.Table<SyncPending>().Count(x=>x.EntityType==change.EntityType&&x.ClientId==change.ClientId)>0)continue;
                ApplyChange(c,change);
            }
            RecalculateStatuses(c);
            c.Execute("UPDATE SyncControl SET Suppressed=0,Cursor=?,LastSyncUtc=? WHERE Id=1",page.Cursor,DateTime.UtcNow);
        });
    }
    public Task ReplaceSnapshotAsync(IReadOnlyList<SyncChange> changes) => _db.RunInTransactionAsync(c =>
    {
        c.Execute("UPDATE SyncControl SET Suppressed=1 WHERE Id=1");
        foreach(var (table,_) in SyncTables.Reverse())c.Execute($"DELETE FROM {table}");
        c.DeleteAll<SyncPending>();
        c.DeleteAll<SyncEntityRevision>();
        foreach(var change in changes.Where(x=>!x.Deleted).OrderBy(x=>Order(x.EntityType))){ApplyChange(c,change);RecordRevision(c,change);}
        RecalculateStatuses(c);
        c.Execute("UPDATE SyncControl SET Suppressed=0,Initialized=1,NeedsReconciliation=0,Cursor=0 WHERE Id=1");
    });
    public Task ArchiveConflictAsync(SyncConflict conflict) => _db.RunInTransactionAsync(c =>
    {
        var pending = c.Table<SyncPending>().FirstOrDefault(x => x.OperationId == conflict.OperationId);
        if (pending is null) return;
        foreach (var attempt in c.Table<SyncPending>().Where(x => x.EntityType == pending.EntityType && x.ClientId == pending.ClientId).ToList())
        {
            var payload = attempt.Payload ?? JsonSerializer.Serialize(MakeChange(c, attempt), SyncJson);
            c.InsertOrReplace(new SyncRejected { OperationId = attempt.OperationId, EntityType = attempt.EntityType,
                ClientId = attempt.ClientId, Payload = payload, Reason = conflict.Reason, RejectedAtUtc = DateTime.UtcNow });
            c.Delete(attempt);
        }
        c.Execute("UPDATE SyncControl SET Suppressed=1 WHERE Id=1");
        var authoritative = conflict.Current ?? new SyncChange(Guid.Empty, conflict.EntityType, conflict.ClientId, true, null);
        ApplyChange(c, authoritative);
        RecordRevision(c, authoritative);
        RecalculateStatuses(c);
        c.Execute("UPDATE SyncControl SET Suppressed=0 WHERE Id=1");
    });
    public Task ArchiveBootstrapConflictAsync(SyncChange change) => _db.InsertOrReplaceAsync(new SyncRejected
    {
        OperationId = change.OperationId, EntityType = change.EntityType, ClientId = change.ClientId,
        Payload = JsonSerializer.Serialize(change, SyncJson),
        Reason = "Conciliação inicial: a versão do servidor foi preservada.", RejectedAtUtc = DateTime.UtcNow
    });
    public Task ArchiveCommandConflictAsync(SyncCommandPending pending, string reason, SyncChange? current) => _db.RunInTransactionAsync(c =>
    {
        var command = JsonSerializer.Deserialize<FinanceCommand>(pending.Payload, SyncJson)!;
        c.InsertOrReplace(new SyncRejected { OperationId = pending.OperationId, EntityType = command.EntityType,
            ClientId = command.ClientId, Payload = pending.Payload, Reason = reason, RejectedAtUtc = DateTime.UtcNow });
        c.Delete(pending);
        if (current is null) return;
        c.Execute("UPDATE SyncControl SET Suppressed=1 WHERE Id=1");
        ApplyChange(c, current);
        RecordRevision(c, current);
        RecalculateStatuses(c);
        c.Execute("UPDATE SyncControl SET Suppressed=0 WHERE Id=1");
    });
    private static void RecordRevision(SQLiteConnection c, SyncChange change)
    {
        if (change.Revision.HasValue)
            c.InsertOrReplace(new SyncEntityRevision { Key = change.EntityType + ":" + change.ClientId, Revision = change.Revision.Value });
    }
    private static int Parent<T>(SQLiteConnection c,Guid? id) where T:new()
    {
        var found = c.ExecuteScalar<int>($"SELECT Id FROM {typeof(T).Name} WHERE ClientId=?",id);
        return found>0 ? found : throw new InvalidOperationException("Vínculo remoto não encontrado. A sincronização será tentada novamente.");
    }
    private static void Upsert<T>(SQLiteConnection c,T entity,Guid id) where T:new()
    {
        var localId=c.ExecuteScalar<int>($"SELECT Id FROM {typeof(T).Name} WHERE ClientId=?",id);
        typeof(T).GetProperty("Id")!.SetValue(entity,localId);
        if(localId>0)c.Update(entity);else c.Insert(entity);
    }
    private static void ApplyChange(SQLiteConnection c,SyncChange change)
    {
        var table=SyncTables.Single(x=>x.Type==change.EntityType).Table;
        if(change.Deleted)
        {
            c.InsertOrReplace(new SyncDeleted{Key=change.EntityType+":"+change.ClientId.ToString()});
            if(change.EntityType=="Expense")c.Execute("DELETE FROM Transacao WHERE DespesaId IN (SELECT Id FROM Despesa WHERE ClientId=?)",change.ClientId);
            c.Execute($"DELETE FROM {table} WHERE ClientId=?",change.ClientId);return;
        }
        switch(change.EntityType)
        {
            case "FixedIncome": var fi=change.Data!.Value.Deserialize<FixedIncome>(SyncJson)!;Upsert(c,new ReceitaFixa{ClientId=fi.ClientId,FontePagadora=fi.Payer,Descricao=fi.Description,Tipo=fi.Type,Valor=fi.Amount,Ativa=fi.Active},change.ClientId);break;
            case "FixedExpense": var fe=change.Data!.Value.Deserialize<FixedExpense>(SyncJson)!;Upsert(c,new DespesaFixa{ClientId=fe.ClientId,ReceitaFixaId=Parent<ReceitaFixa>(c,change.ParentClientId),Descricao=fe.Description,Categoria=fe.Category,Valor=fe.Amount,DiaVencimento=fe.DueDay,Ativa=fe.Active},change.ClientId);break;
            case "Income": var i=change.Data!.Value.Deserialize<Income>(SyncJson)!;Upsert(c,new Receita{ClientId=i.ClientId,MesReferencia=Time(i.ReferenceMonth),FontePagadora=i.Payer,Descricao=i.Description,Tipo=i.Type,Valor=i.Amount,ReceitaFixaId=change.FixedClientId.HasValue?Parent<ReceitaFixa>(c,change.FixedClientId):null,CompetenciaGeracao=Time(i.GeneratedForMonth)},change.ClientId);break;
            case "Expense": var e=change.Data!.Value.Deserialize<Expense>(SyncJson)!;Upsert(c,new Despesa{ClientId=e.ClientId,ReceitaId=Parent<Receita>(c,change.ParentClientId),DataCadastro=Time(e.CreatedOn),Vencimento=Time(e.DueDate),NParcela=e.InstallmentNumber,Descricao=e.Description,Categoria=e.Category,Valor=e.Amount,replicavel=e.Replicable,DespesaFixaId=change.FixedClientId.HasValue?Parent<DespesaFixa>(c,change.FixedClientId):null,CompetenciaGeracao=Time(e.GeneratedForMonth)},change.ClientId);break;
            case "Payment": var p=change.Data!.Value.Deserialize<Payment>(SyncJson)!;Upsert(c,new Transacao{ClientId=p.ClientId,DespesaId=Parent<Despesa>(c,change.ParentClientId),Observacao=p.Note,Valor=p.Amount,Data=Time(p.Date)},change.ClientId);break;
            case "MonthlyPreparationItem":
                var m=change.Data!.Value.Deserialize<MonthlyPreparationItem>(SyncJson)!;
                var local=new PreparacaoMensalItem{ClientId=m.ClientId,TipoModelo=m.ModelType=="INCOME"?"RECEITA":"DESPESA",ModeloId=m.ModelType=="INCOME"?Parent<ReceitaFixa>(c,change.ParentClientId):Parent<DespesaFixa>(c,change.ParentClientId),Competencia=Time(m.ReferenceMonth),Estado=m.State=="GENERATED"?"GERADO":"IGNORADO",AtualizadoEm=m.UpdatedAtUtc};
                var existing=c.Table<PreparacaoMensalItem>().FirstOrDefault(x=>x.TipoModelo==local.TipoModelo&&x.ModeloId==local.ModeloId&&x.Competencia==local.Competencia);
                if(existing is not null){local.Id=existing.Id;c.Update(local);}else Upsert(c,local,change.ClientId);break;
        }
    }
    private static void RecalculateStatuses(SQLiteConnection c)
    {
        foreach(var expense in c.Table<Despesa>().ToList())
        {
            var paid=c.Table<Transacao>().Where(x=>x.DespesaId==expense.Id).ToList().Sum(x=>x.Valor);
            expense.Sttatus=paid>=expense.Valor?true:paid>0?false:null;c.Update(expense);
        }
    }
    private static DateOnly? Date(DateTime? d)=>d.HasValue?DateOnly.FromDateTime(d.Value):null;
    private static DateTime Time(DateOnly d)=>d.ToDateTime(TimeOnly.MinValue);
    private static DateTime? Time(DateOnly? d)=>d?.ToDateTime(TimeOnly.MinValue);
}
