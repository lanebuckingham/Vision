using Microsoft.EntityFrameworkCore;
using Vision.CredentialService.Domain;
using Vision.CredentialService.Infrastructure.Persistence;
using Vision.CredentialService.Infrastructure.Persistence.Seeding;

namespace Vision.CredentialService.Tests.Infrastructure;

/// <summary>
/// Verifies that repeated seeder execution does not add duplicates.
/// Uses its own isolated database to avoid interference with other tests.
/// </summary>
public class SeedIdempotencyTests : IAsyncLifetime
{
    private const string ConnectionString = "Host=localhost;Database=vision_credential_seed_test;Username=vision;Password=vision_dev";

    private CredentialDbContext CreateDbContext()
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

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotDuplicateData()
    {
        // First seed
        await using var db1 = CreateDbContext();
        await CredentialSeeder.SeedAsync(db1);

        var peopleCountAfterFirst = await db1.People.CountAsync();
        var credentialCountAfterFirst = await db1.Credentials.CountAsync();

        Assert.True(peopleCountAfterFirst > 0, "Seeder should create people");
        Assert.True(credentialCountAfterFirst > 0, "Seeder should create credentials");

        // Second seed — should not add duplicates
        await using var db2 = CreateDbContext();
        await CredentialSeeder.SeedAsync(db2);

        var peopleCountAfterSecond = await db2.People.CountAsync();
        var credentialCountAfterSecond = await db2.Credentials.CountAsync();

        Assert.Equal(peopleCountAfterFirst, peopleCountAfterSecond);
        Assert.Equal(credentialCountAfterFirst, credentialCountAfterSecond);
    }

    [Fact]
    public async Task SeedAsync_RunTwice_MichaelBrownRemainsSingular()
    {
        await using var db1 = CreateDbContext();
        await CredentialSeeder.SeedAsync(db1);

        await using var db2 = CreateDbContext();
        await CredentialSeeder.SeedAsync(db2);

        // Verify Michael Brown appears exactly once
        var michaelCount = await db2.People
            .CountAsync(p => p.FirstName == "Michael" && p.LastName == "Brown");
        Assert.Equal(1, michaelCount);

        // Verify the lost-badge credential is singular
        var lostBadgeCount = await db2.Credentials
            .CountAsync(c => c.Id == SeedDataIds.CredentialLostBadge);
        Assert.Equal(1, lostBadgeCount);
    }

    [Fact]
    public async Task SeedAsync_LostBadgeCredential_IsNotRevoked()
    {
        await using var db = CreateDbContext();
        await CredentialSeeder.SeedAsync(db);

        var lostBadge = await db.Credentials
            .FirstOrDefaultAsync(c => c.Id == SeedDataIds.CredentialLostBadge);

        Assert.NotNull(lostBadge);
        Assert.Null(lostBadge!.RevokedAt);
        Assert.Equal(CredentialStatus.Active, lostBadge.Status);
    }
}
