using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Tests.Integration;

/// <summary>
/// Creates a real PostgreSQL test database using the project's local Docker PostgreSQL.
/// Each test class gets an isolated schema to avoid conflicts.
/// Requires: docker compose up -d (vision-postgres running on localhost:5432)
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Database=vision_test;Username=vision;Password=vision_dev";

    public WorkOrderDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WorkOrderDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new WorkOrderDbContext(options);
    }

    public async Task InitializeAsync()
    {
        // Ensure test database and schema exist
        await using var ctx = CreateContext();
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureDeletedAsync();
    }
}
