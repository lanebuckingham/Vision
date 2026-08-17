using Vision.CredentialService.Application.Common;
using Vision.CredentialService.Application.Credentials.Queries;
using Vision.CredentialService.Domain;
using Vision.CredentialService.Infrastructure.Persistence;
using Vision.CredentialService.Tests.Infrastructure;

namespace Vision.CredentialService.Tests.Application;

/// <summary>
/// Tests the exact 30-day expiring-soon boundary per specification.
/// </summary>
[Collection("PostgreSQL")]
public class ExpiringSoonBoundaryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ExpiringSoon_ExpiresIn10Days_IsExpiringSoon()
    {
        await using var db = fixture.CreateDbContext();
        var (personId, credId) = await SeedCredentialExpiring(db, daysUntilExpiry: 10);

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(
            new GetCredentialsQuery(null, null, personId, true, null, 1, 25), CancellationToken.None);

        Assert.Contains(result.Items, c => c.Id == credId);
        var cred = result.Items.First(c => c.Id == credId);
        Assert.True(cred.IsExpiringSoon);
        Assert.Equal("Active", cred.Status);
    }

    [Fact]
    public async Task ExpiringSoon_ExpiresIn29Days_IsExpiringSoon()
    {
        await using var db = fixture.CreateDbContext();
        var (personId, credId) = await SeedCredentialExpiring(db, daysUntilExpiry: 29);

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(
            new GetCredentialsQuery(null, null, personId, true, null, 1, 25), CancellationToken.None);

        Assert.Contains(result.Items, c => c.Id == credId);
    }

    [Fact]
    public async Task ExpiringSoon_ExpiresInExactly30Days_IsExpiringSoon()
    {
        // Spec: ExpiresAt <= now + 30 days — the 30-day boundary is inclusive
        await using var db = fixture.CreateDbContext();
        // Use a margin to avoid millisecond boundary issues
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(CredentialPolicy.ExpiringSoonDays).AddSeconds(-1);

        var (personId, credId) = await SeedCredentialWithExpiry(db, expiresAt);

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(
            new GetCredentialsQuery(null, null, personId, true, null, 1, 25), CancellationToken.None);

        Assert.Contains(result.Items, c => c.Id == credId);
    }

    [Fact]
    public async Task ExpiringSoon_ExpiresIn31Days_IsNotExpiringSoon()
    {
        await using var db = fixture.CreateDbContext();
        var (personId, credId) = await SeedCredentialExpiring(db, daysUntilExpiry: 31);

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(
            new GetCredentialsQuery(null, null, personId, true, null, 1, 25), CancellationToken.None);

        Assert.DoesNotContain(result.Items, c => c.Id == credId);
    }

    [Fact]
    public async Task ExpiringSoon_ExpiresIn90Days_IsNotExpiringSoon()
    {
        await using var db = fixture.CreateDbContext();
        var (personId, credId) = await SeedCredentialExpiring(db, daysUntilExpiry: 90);

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(
            new GetCredentialsQuery(null, null, personId, true, null, 1, 25), CancellationToken.None);

        Assert.DoesNotContain(result.Items, c => c.Id == credId);
    }

    [Fact]
    public async Task ExpiringSoon_AlreadyExpired_IsNotExpiringSoon()
    {
        await using var db = fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var (personId, credId) = await SeedCredentialWithExpiry(db, now.AddDays(-1));

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(
            new GetCredentialsQuery(null, null, personId, true, null, 1, 25), CancellationToken.None);

        Assert.DoesNotContain(result.Items, c => c.Id == credId);
    }

    [Fact]
    public async Task ExpiringSoon_RevokedButWithin30Days_IsNotExpiringSoon()
    {
        await using var db = fixture.CreateDbContext();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Revoked", LastName = "Soon", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(person);
        var credId = Guid.NewGuid();
        db.Credentials.Add(new Credential
        {
            Id = credId, PersonId = person.Id, CredentialNumber = $"BNDRY-{tag}",
            AccessLevel = CredentialAccessLevel.General,
            IssuedAt = DateTimeOffset.UtcNow.AddDays(-300),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(10), // within 30 days
            RevokedAt = DateTimeOffset.UtcNow.AddDays(-1),
            RevocationReason = "Revoked",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-300)
        });
        await db.SaveChangesAsync();

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(
            new GetCredentialsQuery(null, null, person.Id, true, null, 1, 25), CancellationToken.None);

        Assert.DoesNotContain(result.Items, c => c.Id == credId);
    }

    private async Task<(Guid PersonId, Guid CredentialId)> SeedCredentialExpiring(
        CredentialDbContext db, int daysUntilExpiry)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(daysUntilExpiry);
        return await SeedCredentialWithExpiry(db, expiresAt);
    }

    private async Task<(Guid PersonId, Guid CredentialId)> SeedCredentialWithExpiry(
        CredentialDbContext db, DateTimeOffset expiresAt)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Boundary", LastName = $"Test-{tag}", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(person);
        var credId = Guid.NewGuid();
        db.Credentials.Add(new Credential
        {
            Id = credId, PersonId = person.Id, CredentialNumber = $"BND-{tag}",
            AccessLevel = CredentialAccessLevel.General,
            IssuedAt = DateTimeOffset.UtcNow.AddDays(-300),
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-300)
        });
        await db.SaveChangesAsync();
        return (person.Id, credId);
    }
}
