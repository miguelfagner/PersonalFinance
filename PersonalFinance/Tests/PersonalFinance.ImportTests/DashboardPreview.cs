using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Contracts;
using PersonalFinance.Api.Data;
using PersonalFinance.Api.Services;

internal static class DashboardPreview
{
    internal static async Task RunAsync()
    {
        var root=Path.GetFullPath("Web/artifacts/client/wwwroot");
        var directory=Path.GetFullPath(Path.Combine("Tests","PersonalFinance.ImportTests","obj","preview",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(directory);
        var database="PersonalFinancePreview_"+Guid.NewGuid().ToString("N");
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{Args=[],WebRootPath=root});
        builder.WebHost.UseUrls("http://127.0.0.1:5088");
        builder.Services.AddDbContext<FinanceDbContext>(o=>o.UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={database};AttachDbFilename={Path.Combine(directory,"preview.mdf")};Trusted_Connection=True;TrustServerCertificate=True"));
        builder.Services.AddScoped<FinanceService>();builder.Services.AddScoped<SyncService>();builder.Services.AddScoped<BackupService>();builder.Services.AddScoped<SqliteImporter>();
        var app=builder.Build();
        app.Use(async(context,next)=>{try{await next(context);}catch(Exception e)when(e is ArgumentException or InvalidOperationException or KeyNotFoundException){await Results.Problem(statusCode:e is KeyNotFoundException?404:400,detail:e.Message).ExecuteAsync(context);}});
        app.UseStaticFiles(new StaticFileOptions{FileProvider=new PhysicalFileProvider(root),ServeUnknownFileTypes=true});
        app.MapPost("/api/auth/login",()=>Results.Ok(new LoginResponse("synthetic-preview-token",DateTime.UtcNow.AddHours(1))));
        app.MapGroup("/api").MapFinance();
        app.MapFallbackToFile("index.html",new StaticFileOptions{FileProvider=new PhysicalFileProvider(root)});
        using(var scope=app.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<FinanceDbContext>();await db.Database.MigrateAsync();
            var finance=scope.ServiceProvider.GetRequiredService<FinanceService>();
            var month=new DateOnly(2026,10,1);
            var income=await finance.SaveAsync(new Income{ReferenceMonth=month,Payer="Salário",Description="Receita mensal",Type="SALARIO",Amount=3000},default);
            foreach(var fixture in DashboardRegressionTests.Fixture(month))
            {
                var payments=fixture.Payments;fixture.Payments=[];fixture.Id=0;fixture.IncomeId=income.Id;fixture.CreatedOn=month;await finance.SaveAsync(fixture,default);
                foreach(var payment in payments){payment.Id=0;payment.ExpenseId=fixture.Id;await finance.SaveAsync(payment,default);}
            }
            var model=await finance.SaveAsync(new FixedIncome{Payer="Salário",Description="Modelo de salário",Type="SALARIO",Amount=3000},default);
            await finance.SaveAsync(new FixedExpense{FixedIncomeId=model.Id,Description="Aluguel",Category="MORADIA",Amount=700,DueDay=10},default);
        }
        try{await app.RunAsync();}
        finally{using var scope=app.Services.CreateScope();await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Database.EnsureDeletedAsync();}
    }
}
