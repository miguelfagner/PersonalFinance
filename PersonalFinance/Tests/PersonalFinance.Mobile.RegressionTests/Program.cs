using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using PersonalFinance.Contracts;
using System.Text.Json;

var directory=Path.GetFullPath(Path.Combine("Tests","PersonalFinance.Mobile.RegressionTests","obj","regression",Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(directory);
Android.App.Application.Context=new(directory);
var startupContext = new StartupSynchronizationContext();
var previousContext = SynchronizationContext.Current;
DatabaseService db;
try
{
    SynchronizationContext.SetSynchronizationContext(startupContext);
    db = new DatabaseService();
}
finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
Assert(startupContext.Posts == 0, "Database startup must not depend on the blocked UI context");
var income=new Receita{FontePagadora="Salário",Descricao="Mensal",Tipo="SALARIO",Valor=3000};
await db.SalvarReceitaAsync(income);
var expense=new Despesa{ReceitaId=income.Id,Descricao="Pessoal",Categoria="PESSOAL",Valor=100,Vencimento=new(2026,10,15),DataCadastro=new(2026,10,1)};
await db.SalvarDespesaAsync(expense);
await db.SalvarTransacaoAsync(new(){DespesaId=expense.Id,Data=new(2026,10,4),Valor=60,Observacao="Compra 1"});
await db.SalvarTransacaoAsync(new(){DespesaId=expense.Id,Data=new(2026,10,4),Valor=60,Observacao="Compra 2"});
Assert((await db.ListaTransacoesAsync(expense.Id)).Count==2,"Distinct same-day equal-value payments");
await db.AtualizaStatusAsync(expense.Id);
Assert((await db.PegarDespesaAsync(expense.Id)).Valor==120,"Android overpayment increases expense");
await Throws(()=>db.DeletarReceitaAsync(income),"Income with dependencies");
var pending=await db.FreezePendingAsync();
Assert(pending.Count==4,"Atomic queue coalesces income, expense and two payments");
Assert(pending.All(x=>x.OperationId!=Guid.Empty&&x.ClientId!=Guid.Empty),"Stable identities in trigger queue");
var frozen=pending.Select(x=>x.Payload).ToArray();
Assert((await db.FreezePendingAsync()).Select(x=>x.Payload).SequenceEqual(frozen),"Frozen retry payloads remain identical");
var outgoing=pending.Select(x=>JsonSerializer.Deserialize<SyncChange>(x.Payload!,new JsonSerializerOptions(JsonSerializerDefaults.Web))!).ToList();
Assert(outgoing.Where(x=>x.EntityType=="Payment").All(x=>x.ParentClientId==expense.ClientId),"Relations travel by UUID");
await db.AcceptPendingAsync(pending.Select(x=>x.OperationId));
var fixedIncome=new ReceitaFixa{FontePagadora="Salário",Descricao="Base",Tipo="SALARIO",Valor=3000};
await db.SalvarReceitaFixaAsync(fixedIncome);
var fixedExpense=new DespesaFixa{ReceitaFixaId=fixedIncome.Id,Descricao="Aluguel",Categoria="MORADIA",Valor=700,DiaVencimento=31};
await db.SalvarDespesaFixaAsync(fixedExpense);
await db.PausarReceitaFixaAsync(fixedIncome.Id);
Assert(!(await db.PegarDespesaFixaAsync(fixedExpense.Id))!.Ativa,"Pause cascades");
await db.PausarReceitaFixaAsync(fixedIncome.Id,false);
Assert(!(await db.PegarDespesaFixaAsync(fixedExpense.Id))!.Ativa,"Resume does not cascade");
await db.PausarDespesaFixaAsync(fixedExpense.Id,false);
var month=new DateTime(2024,2,1);
var preview=await db.PreverPreparacaoMensalAsync(month);
preview.Single(x=>x.TipoModelo=="DESPESA").Valor=750;
var generated=await db.PrepararMesAsync(month,preview);
Assert(generated.ReceitasCriadas==1&&generated.DespesasCriadas==1,"Preparation creates linked records");
var prepared=(await db.ListaDespesasAsync(month,month.AddMonths(1))).Single();
Assert(prepared.Vencimento.Day==29&&prepared.Valor==750,"Leap month and monthly override");
Assert((await db.PegarDespesaFixaAsync(fixedExpense.Id))!.Valor==700,"Monthly override preserves master");
var repeated=await db.PrepararMesAsync(month,await db.PreverPreparacaoMensalAsync(month));
Assert(repeated.DespesasCriadas==0&&repeated.ReceitasCriadas==0,"Repeated preparation is idempotent");
prepared.Vencimento=month.AddMonths(1);
await Throws(()=>db.SalvarDespesaAsync(prepared),"Generated competence cannot move");
var rollbackMonth = new DateTime(2030,11,1);
var invalidPreview=await db.PreverPreparacaoMensalAsync(rollbackMonth);
invalidPreview.Single(x=>x.TipoModelo=="DESPESA").Valor=-1;
var before=await db.PendingSyncCountAsync();
await Throws(()=>db.PrepararMesAsync(rollbackMonth,invalidPreview),"Preparation validates and rolls back");
var afterReceipts = (await db.ListaReceitasAsync(rollbackMonth,rollbackMonth.AddMonths(1))).Count;
var afterPending = await db.PendingSyncCountAsync();
Assert(afterReceipts==0,$"Transaction rollback leaves no income (actual {afterReceipts})");
Assert(before==afterPending,$"Transaction rollback includes queue (before {before}, after {afterPending})");
var backup=Path.Combine(directory,"consistent.db");
await db.CreateConsistentBackupAsync(backup);
Assert(File.Exists(backup),"Consistent SQLite backup");
var second=Path.Combine(directory,"receiver");Directory.CreateDirectory(second);Android.App.Application.Context=new(second);
var receiver=new DatabaseService();
await receiver.ApplyPullAsync(new(10,outgoing,false));
var receivedExpense=(await receiver.ListaDespesasAsync(new(2026,10,1),new(2026,11,1))).Single();
Assert(receivedExpense.ClientId==expense.ClientId&&receivedExpense.Valor==120,"Pull preserves identity and overpayment");
Assert((await receiver.ListaTransacoesAsync(receivedExpense.Id)).Count==2&&await receiver.PendingSyncCountAsync()==0,"Pull creates no echo");
await receiver.ApplyPullAsync(new(11,[new(Guid.Empty,"Expense",expense.ClientId,true,null)],false));
Assert((await receiver.ListaTransacoesAsync(receivedExpense.Id)).Count==0,"Remote deletion removes child payments");
var firstCommand=new FinanceCommand(Guid.NewGuid(),"Upsert","Income",income.ClientId,10,
    JsonSerializer.SerializeToElement(new Income{ClientId=income.ClientId,ReferenceMonth=new(2026,10,1),Payer="Novo",Description="Mensal",Type="SALARIO",Amount=3100}));
var secondCommand=firstCommand with{OperationId=Guid.NewGuid(),DependsOn=firstCommand.OperationId,ExpectedRevision=10};
var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
var mobileJson=new JsonSerializerOptions(JsonSerializerDefaults.Web){TypeInfoResolver=FinanceJsonContext.Default};
var commandResult=new FinanceCommandResult("Applied")
    { Revisions=new Dictionary<Guid,long>{{income.ClientId,42}} };
Assert(JsonSerializer.Deserialize<FinanceCommandResult>(JsonSerializer.Serialize(commandResult,mobileJson),mobileJson)!
    .Revisions[income.ClientId]==42,"Android JSON preserves receipt revisions");
await receiver.QueueCommandAsync(new(){OperationId=firstCommand.OperationId,Server="test",Email="test@example.com",Payload=JsonSerializer.Serialize(firstCommand,options)});
await receiver.QueueCommandAsync(new(){OperationId=secondCommand.OperationId,Server="test",Email="test@example.com",Payload=JsonSerializer.Serialize(secondCommand,options)});
await receiver.AcceptCommandAndRebaseAsync(firstCommand.OperationId,new Dictionary<Guid,long>{{income.ClientId,42}});
var rebased=(await receiver.PendingCommandsAsync("test","test@example.com")).Single();
Assert(JsonSerializer.Deserialize<FinanceCommand>(rebased.Payload,options)!.ExpectedRevision==42,"Consecutive offline commands keep server revision order");
var rowFirst = new SyncPending{OperationId=Guid.NewGuid(),EntityType="Income",ClientId=income.ClientId,BaseRevision=10,
    Payload=JsonSerializer.Serialize(new SyncChange(Guid.NewGuid(),"Income",income.ClientId,false,
        JsonSerializer.SerializeToElement(new Income{ClientId=income.ClientId,ReferenceMonth=new(2026,10,1),Payer="Primeira",Description="Mensal",Type="SALARIO",Amount=3100})) { ExpectedRevision=10 },options)};
var rowSecond = new SyncPending{OperationId=Guid.NewGuid(),EntityType="Income",ClientId=income.ClientId,BaseRevision=10,
    Payload=JsonSerializer.Serialize(new SyncChange(Guid.NewGuid(),"Income",income.ClientId,false,
        JsonSerializer.SerializeToElement(new Income{ClientId=income.ClientId,ReferenceMonth=new(2026,10,1),Payer="Segunda",Description="Mensal",Type="SALARIO",Amount=3200})) { ExpectedRevision=10 },options)};
using (var connection = new SQLite.SQLiteConnection(Path.Combine(second,"financeiro.db")))
{ connection.Insert(rowFirst); connection.Insert(rowSecond); }
await receiver.AcceptPendingAndRebaseAsync(new SyncPushResult([rowFirst.OperationId])
    { Revisions=new Dictionary<Guid,long>{{rowFirst.OperationId,43}} });
using (var connection = new SQLite.SQLiteConnection(Path.Combine(second,"financeiro.db")))
{
    var later=connection.Table<SyncPending>().Single(x=>x.OperationId==rowSecond.OperationId);
    Assert(JsonSerializer.Deserialize<SyncChange>(later.Payload!,options)!.ExpectedRevision==43,"Consecutive legacy rows use exact committed revision");
    connection.Delete(later);
}
using (var connection = new SQLite.SQLiteConnection(Path.Combine(second,"financeiro.db")))
    connection.Execute("UPDATE SyncControl SET Suppressed=1 WHERE Id=1");
await DatabaseService.InvalidateSyncInitializationAsync();
var restarted = new DatabaseService();
Assert(!(await restarted.GetSyncStateAsync()).Suppressed,"Restart restores offline change capture");
var foreignCommand = new SyncCommandPending { OperationId=Guid.NewGuid(), Server="other", Email="other@example.com", Payload=JsonSerializer.Serialize(firstCommand,options) };
await restarted.QueueCommandAsync(foreignCommand);
var retiredDecision = new PreparacaoMensalItem { TipoModelo="DESPESA", ModeloId=999999, Competencia=new(2026,9,1), Estado="IGNORADO" };
using (var connection = new SQLite.SQLiteConnection(Path.Combine(second,"financeiro.db"))) connection.Insert(retiredDecision);
var checkpoint = await restarted.CaptureDeviceSnapshotAsync(500,"test","test@example.com");
Assert(checkpoint.Request.Changes.Count>0 && !checkpoint.CommandIds.Contains(foreignCommand.OperationId),"First load captures full history and only the current account queue");
var savedCheckpoint = JsonSerializer.Deserialize<DeviceSnapshotCheckpoint>(JsonSerializer.Serialize(checkpoint,mobileJson),mobileJson)!;
Assert(savedCheckpoint.Request.OperationId==checkpoint.Request.OperationId && savedCheckpoint.Request.Changes.Count==checkpoint.Request.Changes.Count,"First load checkpoint survives Android JSON roundtrip");
var localIncome = await restarted.PegarReceitaAsync(await restarted.LocalIdAsync("Receita",income.ClientId));
localIncome.Valor=3333;await restarted.SalvarReceitaAsync(localIncome);
var laterCommand = firstCommand with { OperationId=Guid.NewGuid(), DependsOn=checkpoint.CommandIds.First(), ExpectedRevision=1 };
await restarted.QueueCommandAsync(new(){OperationId=laterCommand.OperationId,Server="test",Email="test@example.com",Payload=JsonSerializer.Serialize(laterCommand,options)});
var confirmedSnapshot = new SyncPullResult(1500,checkpoint.Request.Changes.Where(x=>x.ClientId!=retiredDecision.ClientId)
    .Select((x,index)=>x with{Revision=1000+index}).ToList(),false);
await restarted.CompleteDeviceSnapshotAsync(checkpoint,confirmedSnapshot);
using (var connection = new SQLite.SQLiteConnection(Path.Combine(second,"financeiro.db")))
    Assert(connection.Table<PreparacaoMensalItem>().Any(x=>x.ClientId==retiredDecision.ClientId),"Archived decisions stay intact on the device after acknowledgement");
Assert((await restarted.GetSyncStateAsync()).Initialized && !(await restarted.GetSyncStateAsync()).NeedsReconciliation && (await restarted.GetSyncStateAsync()).Cursor==1500,"First load activates automatic sync at the acknowledged cursor");
Assert((await restarted.PegarReceitaAsync(localIncome.Id)).Valor==3333 && await restarted.PendingSyncCountAsync()==1,"Acknowledgement preserves edits made after the snapshot");
var laterPending=(await restarted.FreezePendingAsync()).Single();
Assert(laterPending.BaseRevision==confirmedSnapshot.Changes.Single(x=>x.ClientId==income.ClientId).Revision,"Later edits are rebased on the snapshot revision");
var laterQueued=JsonSerializer.Deserialize<FinanceCommand>((await restarted.PendingCommandsAsync("test","test@example.com")).Single().Payload,options)!;
Assert(laterQueued.DependsOn is null && laterQueued.ExpectedRevision==laterPending.BaseRevision,"Later commands detach captured predecessors and use the new revision");
Assert((await restarted.PendingCommandsAsync("other","other@example.com")).Single().OperationId==foreignCommand.OperationId,"Other account commands survive first load");
Console.WriteLine("PASS Android first load: complete checkpoint, JSON retry payload, account queues, activation, later edits and command rebasing");
Console.WriteLine("PASS Android: payments, dependencies, fixed models, preparation, rollback, UUID queue, frozen retries, ordered commands, restart recovery, pull without echo, deletion and backup");

static void Assert(bool value,string name){if(!value)throw new Exception("FAIL "+name);}
static async Task Throws(Func<Task> action,string name){try{await action();}catch(Exception e)when(e is InvalidOperationException or ArgumentException){return;}throw new Exception("FAIL expected validation: "+name);}

// Dispatch on the pool so a captured UI continuation fails the assertion instead of hanging the suite.
sealed class StartupSynchronizationContext : SynchronizationContext
{
    private int posts;
    public int Posts => Volatile.Read(ref posts);
    public override void Post(SendOrPostCallback callback, object? state)
    {
        Interlocked.Increment(ref posts);
        ThreadPool.QueueUserWorkItem(_ => callback(state));
    }
}
