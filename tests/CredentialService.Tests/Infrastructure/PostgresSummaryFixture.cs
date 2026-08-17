using Microsoft.EntityFrameworkCore;
using Vision.CredentialService.Infrastructure.Persistence;

namespace Vision.CredentialService.Tests.Infrastructure;

/// <summary>
/// Isolated PostgreSQL fixture for the summary test that needs exact count assertions.
/// Uses a separate database to avoid interference from other test data.
/// </summary>
public class PostgresSummaryFixture
{
    private const string ConnectionString = "Host=localhost;Database=vision_credential_summary_test;Username=vision;Password=vision_dev";

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

    public async Task DisposeAsync()
    {
        await using var ctx = CreateDbContext();
        await ctx.Database.EnsureDeletedAsync();
    }
}
