using Microsoft.Extensions.Logging.Abstractions;
using Vision.CredentialService.Application.Credentials.Commands;
using Vision.CredentialService.Domain;
using Vision.CredentialService.Tests.Infrastructure;

namespace Vision.CredentialService.Tests.Application;

[Collection("PostgreSQL")]
public class RevokeCredentialTests(PostgresFixture fixture)
{
    

    
    

    [Fact]
    public async Task RevokeCredential_ActiveCredential_MarksRevoked()
    {
        await using var db = fixture.CreateDbContext();
        var (_, credId) = await SeedActiveCredential(db);
        var handler = CreateHandler(db);

        var result = await handler.Handle(new RevokeCredentialCommand(credId, "Badge reported lost"), CancellationToken.None);

        Assert.Equal("Revoked", result.Status);
        Assert.NotNull(result.RevokedAt);
        Assert.Equal("Badge reported lost", result.RevocationReason);
    }

    [Fact]
    public async Task RevokeCredential_ExpiredCredential_CanStillBeRevoked()
    {
        await using var db = fixture.CreateDbContext();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Exp", LastName = "Person", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(person);
        var credId = Guid.NewGuid();
        db.Credentials.Add(new Credential { Id = credId, PersonId = person.Id, CredentialNumber = "EXP-REV", AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-400), ExpiresAt = DateTimeOffset.UtcNow.AddDays(-30), CreatedAt = DateTimeOffset.UtcNow.AddDays(-400) });
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new RevokeCredentialCommand(credId, "Administrative revocation"), CancellationToken.None);

        Assert.Equal("Revoked", result.Status);
    }

    [Fact]
    public async Task RevokeCredential_AlreadyRevoked_ReturnsIdempotentSuccess()
    {
        await using var db = fixture.CreateDbContext();
        var (_, credId) = await SeedActiveCredential(db);
        var handler = CreateHandler(db);

        // First revoke
        var first = await handler.Handle(new RevokeCredentialCommand(credId, "First reason"), CancellationToken.None);

        // Second revoke
        var second = await handler.Handle(new RevokeCredentialCommand(credId, "Different reason"), CancellationToken.None);

        Assert.Equal("Revoked", second.Status);
        Assert.Equal(first.RevokedAt, second.RevokedAt); // Original timestamp preserved
        Assert.Equal("First reason", second.RevocationReason); // Original reason preserved
    }

    [Fact]
    public async Task RevokeCredential_UnknownCredential_ThrowsKeyNotFound()
    {
        await using var db = fixture.CreateDbContext();
        var handler = CreateHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(new RevokeCredentialCommand(Guid.NewGuid(), "Test"), CancellationToken.None));
    }

    [Fact]
    public async Task RevokeCredential_BlankReason_ThrowsArgument()
    {
        await using var db = fixture.CreateDbContext();
        var (_, credId) = await SeedActiveCredential(db);
        var handler = CreateHandler(db);

        // The domain Revoke method throws for blank reason
        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.Handle(new RevokeCredentialCommand(credId, "   "), CancellationToken.None));
    }

    private static RevokeCredentialCommandHandler CreateHandler(Vision.CredentialService.Infrastructure.Persistence.CredentialDbContext db) =>
        new(db, NullLogger<RevokeCredentialCommandHandler>.Instance);

    private static async Task<(Guid PersonId, Guid CredentialId)> SeedActiveCredential(Vision.CredentialService.Infrastructure.Persistence.CredentialDbContext db)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Rev", LastName = "Test", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(person);
        var credId = Guid.NewGuid();
        db.Credentials.Add(new Credential { Id = credId, PersonId = person.Id, CredentialNumber = $"REV-{credId:N}"[..20], AccessLevel = CredentialAccessLevel.Clinical, IssuedAt = DateTimeOffset.UtcNow.AddDays(-30), ExpiresAt = DateTimeOffset.UtcNow.AddDays(335), CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) });
        await db.SaveChangesAsync();
        return (person.Id, credId);
    }
}
