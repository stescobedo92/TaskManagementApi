using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskManagementApi.Infrastructure.Data;

namespace TaskManagementApi.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory that provisions an isolated in-memory SQLite connection per test fixture
/// while running the full ASP.NET Core Minimal API and EF Core Migration pipeline.
/// </summary>
public sealed class TaskApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TaskDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<TaskDbContext>>();
            services.RemoveAll<TaskDbContext>();

            services.AddDbContextPool<TaskDbContext>(options =>
                options.UseSqlite(_connection));
        });
    }

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }
}
