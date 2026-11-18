using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManagementApi.Infrastructure.Data;

/// <summary>
/// Design-time factory for EF Core CLI migrations tooling.
/// </summary>
public sealed class DesignTimeTaskDbContextFactory : IDesignTimeDbContextFactory<TaskDbContext>
{
    public TaskDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TaskDbContext>();
        optionsBuilder.UseSqlite("Data Source=tasks.db");
        return new TaskDbContext(optionsBuilder.Options);
    }
}
