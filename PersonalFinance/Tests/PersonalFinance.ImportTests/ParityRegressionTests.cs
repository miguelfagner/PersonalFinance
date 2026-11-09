using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using PersonalFinance.Api.Data;
using PersonalFinance.Api.Services;
using PersonalFinance.Contracts;
using System.Text.Json;

internal static class ParityRegressionTests
{
    internal static async Task RunAsync(FinanceDbContext db, string directory)
    {
        var finance=new FinanceService(db);var sync=new SyncService(db,finance);
        var income=await finance.SaveAsync(new Income{ReferenceMonth=new(2026,10,1),Payer="Pessoa",Description="Salário",Type="SALARIO",Amount=3000},default);
        var expense=await finance.SaveAsync(new Expense{IncomeId=income.Id,CreatedOn=new(2026,10,1),DueDate=new(2026,10,10),Description="Pessoal",Category="PESSOAL",Amount=100},default);
        var payment=new Payment{ExpenseId=expense.Id,Date=new(2026,10,4),Amount=60,Note="Compra"};
        await finance.SaveAsync(payment,default);await finance.SaveAsync(new Payment{ClientId=payment.ClientId,ExpenseId=expense.Id,Date=payment.Date,Amount=60},default);
        Check(await db.Payments.CountAsync()==1,"POST retry does not duplicate");
        await finance.SaveAsync(new Payment{ExpenseId=expense.Id,Date=payment.Date,Amount=60,Note="Outra compra"},default);
        Check((await db.Expenses.FindAsync(expense.Id))!.Amount==120,"Overpayment raises expense");
        var paymentMonth = new DateOnly(2026, 10, 1);
        Check((await finance.PaymentsAsync(paymentMonth, null, "", null, default)).Count == 2,
            "Payments without category filters remain visible");
        Check((await finance.PaymentsAsync(paymentMonth, null, "", null, default, [])).Count == 2,
            "Empty categories from the Blazor All filter do not hide payments");
        Check((await finance.PaymentsAsync(paymentMonth, null, "", null, default, ["", " "])).Count == 2,
            "Blank categories do not hide payments");
        Check((await finance.PaymentsAsync(paymentMonth, null, "", null, default, ["pessoal"])).Count == 2,
            "Selected categories still match normalized payment categories");
        Check((await finance.PaymentsAsync(paymentMonth, null, "", null, default, ["CASA"])).Count == 0,
            "Selected categories still exclude unrelated payments");
        Check((await finance.PaymentsAsync(paymentMonth, null, "CASA", null, default, [])).Count == 0,
            "Single category filter remains effective with an empty category list");
        await Throws(()=>finance.DeleteAsync<Income>(income.Id,default));
        var fixedIncome=await finance.SaveAsync(new FixedIncome{Payer="Pessoa",Description="Base",Type="SALARIO",Amount=3000},default);
        var fixedExpense=await finance.SaveAsync(new FixedExpense{FixedIncomeId=fixedIncome.Id,Description="Aluguel",Category="MORADIA",Amount=700,DueDay=31},default);
        fixedIncome.Active=false;await finance.SaveAsync(fixedIncome,default);
        Check(!(await db.FixedExpenses.FindAsync(fixedExpense.Id))!.Active,"Pause cascades");
        fixedIncome.Active=true;await finance.SaveAsync(fixedIncome,default);
        Check(!(await db.FixedExpenses.FindAsync(fixedExpense.Id))!.Active,"Resume preserves paused expenses");
        fixedExpense.Active=true;await finance.SaveAsync(fixedExpense,default);
        var preview=await finance.PreviewAsync(new(2024,2,1),default);preview.Single(x=>x.ModelType=="EXPENSE").Amount=750;
        var request=new PrepareMonthRequest(new(2024,2,1),[fixedIncome.Id],[fixedExpense.Id]){Items=preview.ToList()};
        await finance.PrepareAsync(request,default);await finance.PrepareAsync(request,default);
        var generated=await db.Expenses.SingleAsync(x=>x.GeneratedForMonth==new DateOnly(2024,2,1));
        Check(generated.Amount==750&&generated.DueDate.Day==29&&fixedExpense.Amount==700,"Monthly override and leap-day clamp");
        Check(await db.MonthlyPreparationItems.CountAsync()==2,"Preparation decisions recorded");
        var snapshot=await finance.PreviewAsync(new(2024,2,1),default);
        Check(snapshot.All(x=>x.AlreadyCreated),"Repeated preview reports existing");
        var next=await finance.PreviewAsync(new(2026,11,1),default);next.Single(x=>x.ModelType=="EXPENSE").Selected=false;
        await finance.PrepareAsync(new(new(2026,11,1),[],[]){Items=next.ToList()},default);
        Check((await finance.PreviewAsync(new(2026,11,1),default)).Single(x=>x.ModelType=="EXPENSE").Ignored,"Ignored choice preserved");
        var invalid=(await finance.PreviewAsync(new(2026,12,1),default)).ToList();invalid.Single(x=>x.ModelType=="EXPENSE").Amount=-1;
        await Throws(()=>finance.PrepareAsync(new(new(2026,12,1),[],[]){Items=invalid},default));
        db.ChangeTracker.Clear();
        Check(!await db.Incomes.AnyAsync(x=>x.GeneratedForMonth==new DateOnly(2026,12,1)),"Preparation rolls back all data and feed");
        var settle=new SettleRequest(new(2024,2,3),"Quitação",Guid.NewGuid());
        await finance.SettleAsync(generated.Id,settle,default);await finance.SettleAsync(generated.Id,settle,default);
        Check(await db.Payments.CountAsync(x=>x.ClientId==settle.OperationId)==1,"Settlement retry");
        var convert=await finance.ConvertAsync(new(new(2026,10,1),[income.Id],[expense.Id]),default);
        Check(convert.IncomesCreated==1&&convert.ExpensesCreated==1,"Convert monthly records");
        var repeatConvert=await finance.ConvertAsync(new(new(2026,10,1),[income.Id],[expense.Id]),default);
        Check(repeatConvert.IncomesCreated==0&&repeatConvert.ExpensesCreated==0,"Convert reuses models");
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var remote=new Income{ReferenceMonth=new(2026,10,1),Payer="Android",Description="Extra",Type="EXTRA",Amount=200,Id=9999};
        var operation=new SyncChange(Guid.NewGuid(),"Income",remote.ClientId,false,JsonSerializer.SerializeToElement(remote,json));
        var first=await sync.PushAsync(new([operation]),default);var again=await sync.PushAsync(new([operation]),default);
        Check(first.Accepted.Count==1&&again.Accepted.Count==1&&await db.Incomes.CountAsync(x=>x.ClientId==remote.ClientId)==1,"Sync retry acknowledgement is idempotent");
        Check(first.Revisions.TryGetValue(operation.OperationId,out var firstRevision)&&
            again.Revisions[operation.OperationId]==firstRevision,"Sync retries return the committed revision");
        var changedSameOperation=await sync.PushAsync(new([operation with{Data=JsonSerializer.SerializeToElement(new Income{ClientId=remote.ClientId,ReferenceMonth=remote.ReferenceMonth,Payer="Outro",Description=remote.Description,Type=remote.Type,Amount=999},json)}]),default);
        Check(changedSameOperation.Accepted.Count==0&&changedSameOperation.Error is not null,"Sync rejects reused operation identity with different content");
        remote.Amount=250;
        var staleEdit=await sync.PushAsync(new([operation with{OperationId=Guid.NewGuid(),Data=JsonSerializer.SerializeToElement(remote,json)}]),default);
        Check(staleEdit.Conflicts.Count==1&&(await db.Incomes.SingleAsync(x=>x.ClientId==remote.ClientId)).Amount==200,"Server rejects stale edit");
        var remoteRevision=(await db.Incomes.SingleAsync(x=>x.ClientId==remote.ClientId)).Revision;
        await sync.PushAsync(new([operation with{OperationId=Guid.NewGuid(),Data=JsonSerializer.SerializeToElement(remote,json),ExpectedRevision=remoteRevision}]),default);
        Check((await db.Incomes.SingleAsync(x=>x.ClientId==remote.ClientId)).Amount==250,"Known revision permits edit");
        var child=new Expense{Description="Android despesa",Category="CASA",Amount=100,CreatedOn=new(2026,10,1),DueDate=new(2026,10,7),IncomeId=9999};
        var childChange=new SyncChange(Guid.NewGuid(),"Expense",child.ClientId,false,JsonSerializer.SerializeToElement(child,json),remote.ClientId);
        Check((await sync.PushAsync(new([childChange]),default)).Error is null,"UUID parent replaces remote integer");
        var childRow=await db.Expenses.SingleAsync(x=>x.ClientId==child.ClientId);
        Check(childRow.IncomeId==(await db.Incomes.SingleAsync(x=>x.ClientId==remote.ClientId)).Id,"Parent remapped");
        var childRevision=(await db.Expenses.SingleAsync(x=>x.ClientId==child.ClientId)).Revision;
        await sync.PushAsync(new([childChange with{OperationId=Guid.NewGuid(),Deleted=true,Data=null,ExpectedRevision=childRevision}]),default);
        var stale=await sync.PushAsync(new([childChange with{OperationId=Guid.NewGuid()}]),default);
        Check(stale.Accepted.Count==0&&stale.Error is not null,"Deletion prevents stale resurrection");
        var commands=new CommandService(db,finance);
        var identityItems = new List<PrepareIdentityItem>
        {
            new("INCOME", fixedIncome.ClientId, true, fixedIncome.Payer, fixedIncome.Description, fixedIncome.Type, "", fixedIncome.Amount, 1),
            new("EXPENSE", fixedExpense.ClientId, true, "", fixedExpense.Description, "", fixedExpense.Category, fixedExpense.Amount, fixedExpense.DueDay)
        };
        var identityPrepare=new FinanceCommand(Guid.NewGuid(),"PrepareIdentity","",Guid.Empty,null,
            JsonSerializer.SerializeToElement(new PrepareIdentityRequest(new(2027,1,1),identityItems),json));
        Check((await commands.ExecuteAsync(identityPrepare,default)).Status=="Applied" &&
            (await commands.ExecuteAsync(identityPrepare,default)).Status=="Applied" &&
            await db.Incomes.CountAsync(x=>x.GeneratedForMonth==new DateOnly(2027,1,1))==1,
            "UUID preparation is idempotent");
        var identityConvert=new FinanceCommand(Guid.NewGuid(),"ConvertIdentity","",Guid.Empty,null,
            JsonSerializer.SerializeToElement(new ConvertIdentityRequest(new(2026,10,1),[income.ClientId],[expense.ClientId]),json));
        Check((await commands.ExecuteAsync(identityConvert,default)).Status=="Applied","UUID conversion resolves local identities");
        var independent=new Payment{ClientId=Guid.NewGuid(),Date=new(2026,10,5),Note="Pagamento independente",Amount=10};
        var expenseRevisionBeforePayment=(await db.Expenses.FindAsync(expense.Id))!.Revision;
        var payCommand=new FinanceCommand(Guid.NewGuid(),"Upsert",nameof(Payment),independent.ClientId,0,
            JsonSerializer.SerializeToElement(independent,json),expense.ClientId);
        var confirmed=await commands.ExecuteAsync(payCommand,default);
        var repeated=await commands.ExecuteAsync(payCommand,default);
        Check(confirmed.Status=="Applied"&&repeated.Status=="Applied"&&await db.Payments.CountAsync(x=>x.ClientId==independent.ClientId)==1,"Command receipt makes retry idempotent");
        Check(confirmed.Revisions.ContainsKey(independent.ClientId)&&confirmed.Revisions.ContainsKey(expense.ClientId),"Payment receipt carries payment and expense revisions");
        var dependentPayment=new Payment{ClientId=Guid.NewGuid(),Date=new(2026,10,6),Note="Dependente",Amount=5};
        var dependentCommand=new FinanceCommand(Guid.NewGuid(),"Upsert",nameof(Payment),dependentPayment.ClientId,0,
            JsonSerializer.SerializeToElement(dependentPayment,json),expense.ClientId,DependsOn:Guid.NewGuid());
        await Throws(()=>commands.ExecuteAsync(dependentCommand,default));
        Check((await commands.ExecuteAsync(dependentCommand with{DependsOn=payCommand.OperationId},default)).Status=="Applied",
            "Dependent command waits for predecessor receipt");
        Check((await db.Expenses.FindAsync(expense.Id))!.Revision>expenseRevisionBeforePayment,"Payment changes the expense revision");
        await Throws(()=>commands.ExecuteAsync(payCommand with{Data=JsonSerializer.SerializeToElement(new Payment{ClientId=independent.ClientId,Date=independent.Date,Note=independent.Note,Amount=11},json)},default));
        var oldPaymentRevision=(await db.Payments.SingleAsync(x=>x.ClientId==independent.ClientId)).Revision;
        var updatedPayment=new Payment{ClientId=independent.ClientId,Date=independent.Date,Note="Atualizado",Amount=11};
        var staleCommand=payCommand with{OperationId=Guid.NewGuid(),ExpectedRevision=oldPaymentRevision-1,
            Data=JsonSerializer.SerializeToElement(updatedPayment,json)};
        Check((await commands.ExecuteAsync(staleCommand,default)).Status=="Conflict","Command rejects outdated payment");
        var feed=await sync.PullAsync(0,default);
        Check(feed.Changes.Any(x=>x.ClientId==child.ClientId&&x.Deleted)&&feed.Cursor>0,"Deletion in incremental feed");
        var status=await sync.StatusAsync(default); await Throws(()=>sync.BootstrapAsync(new(Guid.NewGuid(),[],status.Cursor),default));
        var identities=await db.Incomes.Select(x=>x.ClientId).ToListAsync();
        var bytes=await new BackupService(db).ExportAsync(default);
        var backup=Path.Combine(directory,"parity-export.db");await File.WriteAllBytesAsync(backup,bytes);
        await db.Payments.ExecuteDeleteAsync();await db.Expenses.ExecuteDeleteAsync();await db.Incomes.ExecuteDeleteAsync();await db.FixedExpenses.ExecuteDeleteAsync();await db.FixedIncomes.ExecuteDeleteAsync();await db.MonthlyPreparationItems.ExecuteDeleteAsync();db.ChangeTracker.Clear();
        await using var stream=File.OpenRead(backup);
        await new SqliteImporter(db).ImportAsync(new FormFile(stream,0,stream.Length,"file","financeiro.db"),default);
        Check((await db.Incomes.Select(x=>x.ClientId).ToListAsync()).Order().SequenceEqual(identities.Order()),"Backup preserves UUIDs");
        Check(await db.MonthlyPreparationItems.CountAsync()>0,"Backup preserves preparation decisions");
        Console.WriteLine("PASS API: CRUD constraints, payments, fixed models, monthly decisions, atomic rollback, UUID preparation/conversion, command retries, server-wins revisions, tombstones, bootstrap protection and SQLite roundtrip");
    }
    private static void Check(bool condition,string name){if(!condition)throw new Exception("FAIL "+name);}
    private static async Task Throws(Func<Task> action){try{await action();}catch(Exception e)when(e is ArgumentException or InvalidOperationException){return;}throw new Exception("Expected validation failure");}
}
