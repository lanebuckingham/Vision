using Vision.CredentialService.Application.Common;
using Vision.CredentialService.Application.Credentials.Queries;
using Vision.CredentialService.Domain;
using Vision.CredentialService.Tests.Infrastructure;

namespace Vision.CredentialService.Tests.Application;

[Collection("PostgreSQL")]
public class CredentialQueryTests(PostgresFixture fixture)
{
    

    
    

    [Fact]
    public async Task GetCredentials_StatusActiveFilter_ReturnsOnlyActive()
    {
        await using var db = fixture.CreateDbContext();
        var personId = await SeedPersonWithCredentials(db);

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(new GetCredentialsQuery("Active", null, personId, null, null, 1, 25), CancellationToken.None);

        Assert.All(result.Items, c => Assert.Equal("Active", c.Status));
        Assert.True(result.Items.Count > 0);
    }

    [Fact]
    public async Task GetCredentials_StatusExpiredFilter_ReturnsOnlyExpired()
    {
        await using var db = fixture.CreateDbContext();
        var personId = await SeedPersonWithCredentials(db);

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(new GetCredentialsQuery("Expired", null, personId, null, null, 1, 25), CancellationToken.None);

        Assert.All(result.Items, c => Assert.Equal("Expired", c.Status));
        Assert.True(result.Items.Count > 0);
    }

    [Fact]
    public async Task GetCredentials_StatusRevokedFilter_ReturnsOnlyRevoked()
    {
        await using var db = fixture.CreateDbContext();
        var personId = await SeedPersonWithCredentials(db);

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(new GetCredentialsQuery("Revoked", null, personId, null, null, 1, 25), CancellationToken.None);

        Assert.All(result.Items, c => Assert.Equal("Revoked", c.Status));
        Assert.True(result.Items.Count > 0);
    }

    [Fact]
    public async Task GetCredentials_AccessLevelFilter()
    {
        await using var db = fixture.CreateDbContext();
        var personId = await SeedPersonWithCredentials(db);

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(new GetCredentialsQuery(null, "Security", personId, null, null, 1, 25), CancellationToken.None);

        Assert.All(result.Items, c => Assert.Equal("Security", c.AccessLevel));
        Assert.True(result.Items.Count > 0);
    }

    [Fact]
    public async Task GetCredentials_PersonIdFilter()
    {
        await using var db = fixture.CreateDbContext();
        var personId = await SeedPersonWithCredentials(db);

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(new GetCredentialsQuery(null, null, personId, null, null, 1, 25), CancellationToken.None);

        Assert.All(result.Items, c => Assert.Equal(personId, c.Person.Id));
        Assert.Equal(4, result.TotalCount); // 2 active + 1 expired + 1 revoked
    }

    [Fact]
    public async Task GetCredentials_ExpiringSoonFilter_ReturnsCorrectSet()
    {
        await using var db = fixture.CreateDbContext();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Exp", LastName = "Soon", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(person);

        // Expiring in 10 days (within 30-day window)
        db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = $"EXPS-{tag}-1", AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-300), ExpiresAt = DateTimeOffset.UtcNow.AddDays(10), CreatedAt = DateTimeOffset.UtcNow.AddDays(-300) });
        // Not expiring soon (90 days out)
        db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = $"EXPS-{tag}-2", AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-300), ExpiresAt = DateTimeOffset.UtcNow.AddDays(90), CreatedAt = DateTimeOffset.UtcNow.AddDays(-300) });
        // Already expired
        db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = $"EXPS-{tag}-3", AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-400), ExpiresAt = DateTimeOffset.UtcNow.AddDays(-5), CreatedAt = DateTimeOffset.UtcNow.AddDays(-400) });
        // Revoked (should not appear)
        db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = $"EXPS-{tag}-4", AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-300), ExpiresAt = DateTimeOffset.UtcNow.AddDays(10), RevokedAt = DateTimeOffset.UtcNow, RevocationReason = "Test", CreatedAt = DateTimeOffset.UtcNow.AddDays(-300) });
        await db.SaveChangesAsync();

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(new GetCredentialsQuery(null, null, person.Id, true, null, 1, 25), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.True(result.Items[0].IsExpiringSoon);
    }

    [Fact]
    public async Task GetCredentials_SearchByCredentialNumber()
    {
        await using var db = fixture.CreateDbContext();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Search", LastName = "Test", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(person);
        db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = "SEARCHABLE-XYZ", AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-30), ExpiresAt = DateTimeOffset.UtcNow.AddDays(300), CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) });
        await db.SaveChangesAsync();

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(new GetCredentialsQuery(null, null, null, null, "SEARCHABLE-XYZ", 1, 25), CancellationToken.None);

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetCredentials_SearchByPersonName()
    {
        await using var db = fixture.CreateDbContext();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "UniqueFirst", LastName = "UniqueLast", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(person);
        db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = "PERSONSEARCH-001", AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-30), ExpiresAt = DateTimeOffset.UtcNow.AddDays(300), CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) });
        await db.SaveChangesAsync();

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(new GetCredentialsQuery(null, null, null, null, "UniqueLast", 1, 25), CancellationToken.None);

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetCredentials_Pagination()
    {
        await using var db = fixture.CreateDbContext();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Page", LastName = "Test", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(person);
        for (int i = 0; i < 5; i++)
        {
            db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = $"PAGE-{Guid.NewGuid():N}"[..20], AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-30), ExpiresAt = DateTimeOffset.UtcNow.AddDays(300 + i), CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) });
        }
        await db.SaveChangesAsync();

        var handler = new GetCredentialsQueryHandler(db);
        var result = await handler.Handle(new GetCredentialsQuery(null, null, person.Id, null, null, 1, 2), CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task GetCredentialById_ReturnsDetail()
    {
        await using var db = fixture.CreateDbContext();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Detail", LastName = "Cred", PersonType = PersonType.Employee, IsActive = true, Email = "test@test.com", Department = "IT", CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(person);
        var credId = Guid.NewGuid();
        db.Credentials.Add(new Credential { Id = credId, PersonId = person.Id, CredentialNumber = "DETAIL-001", AccessLevel = CredentialAccessLevel.Restricted, IssuedAt = DateTimeOffset.UtcNow.AddDays(-30), ExpiresAt = DateTimeOffset.UtcNow.AddDays(300), CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) });
        await db.SaveChangesAsync();

        var handler = new GetCredentialByIdQueryHandler(db);
        var result = await handler.Handle(new GetCredentialByIdQuery(credId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("DETAIL-001", result.CredentialNumber);
        Assert.Equal("Restricted", result.AccessLevel);
        Assert.Equal("Active", result.Status);
        Assert.Equal("Detail Cred", result.Person.DisplayName);
    }

    [Fact]
    public async Task GetCredentialById_UnknownId_ReturnsNull()
    {
        await using var db = fixture.CreateDbContext();

        var handler = new GetCredentialByIdQueryHandler(db);
        var result = await handler.Handle(new GetCredentialByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCredentialSummary_ReturnsCorrectCounts()
    {
        // This test uses a dedicated fixture with known-clean state for exact count assertions.
        // The summary endpoint counts all credentials globally, so we need isolation.
        var summaryFixture = new PostgresSummaryFixture();
        await summaryFixture.InitializeAsync();

        try
        {
            await using var db = summaryFixture.CreateDbContext();
            var person = new Person { Id = Guid.NewGuid(), FirstName = "Sum", LastName = "Test", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
            db.People.Add(person);

            var tag = Guid.NewGuid().ToString("N")[..8];
            // 2 active (1 expiring soon within 30 days)
            db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = $"SUM-A1-{tag}", AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-30), ExpiresAt = DateTimeOffset.UtcNow.AddDays(300), CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) });
            db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = $"SUM-A2-{tag}", AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-300), ExpiresAt = DateTimeOffset.UtcNow.AddDays(15), CreatedAt = DateTimeOffset.UtcNow.AddDays(-300) });
            // 1 expired
            db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = $"SUM-E1-{tag}", AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-400), ExpiresAt = DateTimeOffset.UtcNow.AddDays(-10), CreatedAt = DateTimeOffset.UtcNow.AddDays(-400) });
            // 1 revoked
            db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = $"SUM-R1-{tag}", AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-100), ExpiresAt = DateTimeOffset.UtcNow.AddDays(200), RevokedAt = DateTimeOffset.UtcNow.AddDays(-5), RevocationReason = "Lost", CreatedAt = DateTimeOffset.UtcNow.AddDays(-100) });
            await db.SaveChangesAsync();

            var handler = new GetCredentialSummaryQueryHandler(db);
            var result = await handler.Handle(new GetCredentialSummaryQuery(), CancellationToken.None);

            Assert.Equal(2, result.ActiveCount);       // SUM-A1 + SUM-A2
            Assert.Equal(1, result.ExpiringSoonCount);  // SUM-A2 only (15 days < 30)
            Assert.Equal(1, result.ExpiredCount);       // SUM-E1
            Assert.Equal(1, result.RevokedCount);       // SUM-R1

            // Spec: expiringSoon is a subset of active
            Assert.True(result.ExpiringSoonCount <= result.ActiveCount);
        }
        finally
        {
            await summaryFixture.DisposeAsync();
        }
    }

    private async Task<Guid> SeedPersonWithCredentials(Vision.CredentialService.Infrastructure.Persistence.CredentialDbContext db)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Cred", LastName = "Owner", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(person);

        // Active
        db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = $"ACT-{tag}-1", AccessLevel = CredentialAccessLevel.Clinical, IssuedAt = DateTimeOffset.UtcNow.AddDays(-30), ExpiresAt = DateTimeOffset.UtcNow.AddDays(300), CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) });
        // Active Security
        db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = $"ACT-{tag}-2", AccessLevel = CredentialAccessLevel.Security, IssuedAt = DateTimeOffset.UtcNow.AddDays(-30), ExpiresAt = DateTimeOffset.UtcNow.AddDays(300), CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) });
        // Expired
        db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = $"EXP-{tag}-1", AccessLevel = CredentialAccessLevel.General, IssuedAt = DateTimeOffset.UtcNow.AddDays(-400), ExpiresAt = DateTimeOffset.UtcNow.AddDays(-30), CreatedAt = DateTimeOffset.UtcNow.AddDays(-400) });
        // Revoked
        db.Credentials.Add(new Credential { Id = Guid.NewGuid(), PersonId = person.Id, CredentialNumber = $"REV-{tag}-1", AccessLevel = CredentialAccessLevel.Restricted, IssuedAt = DateTimeOffset.UtcNow.AddDays(-100), ExpiresAt = DateTimeOffset.UtcNow.AddDays(200), RevokedAt = DateTimeOffset.UtcNow.AddDays(-5), RevocationReason = "Test", CreatedAt = DateTimeOffset.UtcNow.AddDays(-100) });
        await db.SaveChangesAsync();

        return person.Id;
    }
}
