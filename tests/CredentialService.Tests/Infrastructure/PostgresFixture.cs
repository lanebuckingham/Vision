using Microsoft.EntityFrameworkCore;
using Vision.CredentialService.Infrastructure.Persistence;

namespace Vision.CredentialService.Tests.Infrastructure;

/// <summary>
/// Shared PostgreSQL fixture for integration tests.
/// Creates the database schema once. Each test uses unique data (GUIDs) for isolation.
/// Requires local Docker PostgreSQL (docker compose up -d postgres).
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private const string ConnectionString = "Host=localhost;Database=vision_credential_tests;Username=vision;Password=vision_dev";

    public CredentialDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CredentialDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CredentialDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var ctx = CreateDbContext();
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
