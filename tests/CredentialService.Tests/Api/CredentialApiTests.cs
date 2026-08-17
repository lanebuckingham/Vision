using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vision.CredentialService.Domain;
using Vision.CredentialService.Infrastructure.Persistence;

namespace Vision.CredentialService.Tests.Api;

[Collection("ApiTests")]
public class CredentialApiTests : IAsyncLifetime
{
    private readonly CredentialServiceFactory _factory = new();
    private HttpClient _client = null!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseReady();
        _factory.SetIdentity("SecurityManager");
        _client = _factory.CreateDefaultClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // === ISSUANCE TESTS ===

    [Fact]
    public async Task IssueCredential_ValidRequest_Returns201()
    {
        var personId = await SeedActivePerson();

        var response = await _client.PostAsJsonAsync($"/api/v1/people/{personId}/credentials", new
        {
            credentialNumber = $"API-{Guid.NewGuid():N}"[..20],
            accessLevel = "Clinical",
            expiresAt = DateTimeOffset.UtcNow.AddDays(365).ToString("o")
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task IssueCredential_UnknownPerson_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/people/{Guid.NewGuid()}/credentials", new
        {
            credentialNumber = "API-404",
            accessLevel = "General",
            expiresAt = DateTimeOffset.UtcNow.AddDays(365).ToString("o")
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task IssueCredential_InactivePerson_Returns409()
    {
        var personId = await SeedInactivePerson();

        var response = await _client.PostAsJsonAsync($"/api/v1/people/{personId}/credentials", new
        {
            credentialNumber = "API-INACTIVE",
            accessLevel = "General",
            expiresAt = DateTimeOffset.UtcNow.AddDays(365).ToString("o")
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task IssueCredential_BlankCredentialNumber_Returns400()
    {
        var personId = await SeedActivePerson();

        var response = await _client.PostAsJsonAsync($"/api/v1/people/{personId}/credentials", new
        {
            credentialNumber = "",
            accessLevel = "General",
            expiresAt = DateTimeOffset.UtcNow.AddDays(365).ToString("o")
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IssueCredential_DuplicateCredentialNumber_Returns409()
    {
        var personId = await SeedActivePerson();
        var credNumber = $"DUP-{Guid.NewGuid():N}"[..20];

        // First issuance
        await _client.PostAsJsonAsync($"/api/v1/people/{personId}/credentials", new
        {
            credentialNumber = credNumber,
            accessLevel = "General",
            expiresAt = DateTimeOffset.UtcNow.AddDays(365).ToString("o")
        });

        // Second issuance with same number
        var response = await _client.PostAsJsonAsync($"/api/v1/people/{personId}/credentials", new
        {
            credentialNumber = credNumber,
            accessLevel = "Clinical",
            expiresAt = DateTimeOffset.UtcNow.AddDays(365).ToString("o")
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task IssueCredential_InvalidAccessLevel_Returns400()
    {
        var personId = await SeedActivePerson();

        var response = await _client.PostAsJsonAsync($"/api/v1/people/{personId}/credentials", new
        {
            credentialNumber = "API-BADLEVEL",
            accessLevel = "SuperAdmin",
            expiresAt = DateTimeOffset.UtcNow.AddDays(365).ToString("o")
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IssueCredential_ExpiredDate_Returns400()
    {
        var personId = await SeedActivePerson();

        var response = await _client.PostAsJsonAsync($"/api/v1/people/{personId}/credentials", new
        {
            credentialNumber = "API-PASTEXP",
            accessLevel = "General",
            expiresAt = DateTimeOffset.UtcNow.AddDays(-10).ToString("o")
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // === REVOCATION TESTS ===

    [Fact]
    public async Task RevokeCredential_ValidActive_Returns200()
    {
        var credId = await SeedActiveCredential();

        var response = await _client.PostAsJsonAsync($"/api/v1/credentials/{credId}/revoke", new
        {
            reason = "Badge reported lost"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Revoked", body.GetProperty("status").GetString());
        Assert.Equal("Badge reported lost", body.GetProperty("revocationReason").GetString());
    }

    [Fact]
    public async Task RevokeCredential_UnknownCredential_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/credentials/{Guid.NewGuid()}/revoke", new
        {
            reason = "Test"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RevokeCredential_BlankReason_Returns400()
    {
        var credId = await SeedActiveCredential();

        var response = await _client.PostAsJsonAsync($"/api/v1/credentials/{credId}/revoke", new
        {
            reason = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RevokeCredential_SecondRevoke_Returns200WithOriginalData()
    {
        var credId = await SeedActiveCredential();

        // First revoke
        var first = await _client.PostAsJsonAsync($"/api/v1/credentials/{credId}/revoke", new
        {
            reason = "Original reason"
        });
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var originalRevokedAt = firstBody.GetProperty("revokedAt").GetString();

        // Second revoke with different reason
        var second = await _client.PostAsJsonAsync($"/api/v1/credentials/{credId}/revoke", new
        {
            reason = "Different reason"
        });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Revoked", secondBody.GetProperty("status").GetString());
        Assert.Equal(originalRevokedAt, secondBody.GetProperty("revokedAt").GetString());
        Assert.Equal("Original reason", secondBody.GetProperty("revocationReason").GetString());
    }

    // === READ TESTS ===

    [Fact]
    public async Task GetPerson_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/people/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCredential_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/credentials/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCredentials_InvalidStatus_Returns400()
    {
        var response = await _client.GetAsync("/api/v1/credentials?status=Invalid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCredentials_InvalidAccessLevel_Returns400()
    {
        var response = await _client.GetAsync("/api/v1/credentials?accessLevel=SuperAdmin");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPeople_InvalidPersonType_Returns400()
    {
        var response = await _client.GetAsync("/api/v1/people?personType=InvalidType");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCredentials_InvalidPage_Returns400()
    {
        var response = await _client.GetAsync("/api/v1/credentials?page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // === HELPERS ===

    private async Task<Guid> SeedActivePerson()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CredentialDbContext>();
        var person = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Api", LastName = "Test",
            PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person.Id;
    }

    private async Task<Guid> SeedInactivePerson()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CredentialDbContext>();
        var person = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Inactive", LastName = "Test",
            PersonType = PersonType.Employee, IsActive = false, CreatedAt = DateTimeOffset.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person.Id;
    }

    private async Task<Guid> SeedActiveCredential()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CredentialDbContext>();
        var person = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Cred", LastName = "Holder",
            PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow
        };
        db.People.Add(person);
        var credential = new Credential
        {
            Id = Guid.NewGuid(), PersonId = person.Id,
            CredentialNumber = $"API-C-{Guid.NewGuid():N}"[..20],
            AccessLevel = CredentialAccessLevel.Clinical,
            IssuedAt = DateTimeOffset.UtcNow.AddDays(-30),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(335),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30)
        };
        db.Credentials.Add(credential);
        await db.SaveChangesAsync();
        return credential.Id;
    }
}
