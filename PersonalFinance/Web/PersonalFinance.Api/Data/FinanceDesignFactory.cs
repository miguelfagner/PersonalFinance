using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PersonalFinance.Api.Data;

// Scaffolding never reads production credentials or starts the application.
public sealed class FinanceDesignFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    public FinanceDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<FinanceDbContext>()
        .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=PersonalFinanceDesignOnly;Integrated Security=true").Options);
}
