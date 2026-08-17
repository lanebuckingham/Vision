using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Vision.CredentialService.Domain;
using Vision.CredentialService.Infrastructure.Persistence;

namespace Vision.CredentialService.Tests.Api;

/// <summary>
/// Authorization integration tests for CredentialService.
/// Uses the TestIdentityStore singleton registered in the factory's TestHeaderAuthHandler.
/// Tests run sequentially within the class (xUnit default).
/// </summary>
[Collection("ApiTests")]
public class CredentialAuthorizationTests : IAsyncLifetime
{
    private readonly CredentialAuthServiceFactory _factory = new();

    public async Task InitializeAsync() => await _factory.EnsureDatabaseReady();
    public Task DisposeAsync() { _factory.Dispose(); return Task.CompletedTask; }

    private void SetIdentity(params string[] roles)
    {
        _factory.SetIdentity(roles);
    }

    private void ClearIdentity()
    {
        _factory.ClearIdentity();
    }

    // === 401 — Unauthenticated ===

    [Theory]
    [InlineData("/api/v1/people")]
    [InlineData("/api/v1/credentials")]
    [InlineData("/api/v1/credentials/summary")]
    public async Task Unauthenticated_Returns401(string path)
    {
        ClearIdentity();
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // === 403 — Technician denied from CredentialService ===

    [Theory]
    [InlineData("/api/v1/people")]
    [InlineData("/api/v1/credentials")]
    [InlineData("/api/v1/credentials/summary")]
    public async Task Technician_Returns403(string path)
    {
        SetIdentity("Technician");
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NoRoleUser_Returns403()
    {
        SetIdentity("SomeOtherRole");
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync("/api/v1/credentials/summary");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // === SecurityManager allowed ===

    [Theory]
    [InlineData("/api/v1/people")]
    [InlineData("/api/v1/credentials")]
    [InlineData("/api/v1/credentials/summary")]
    public async Task SecurityManager_ReadEndpoints_Returns200(string path)
    {
        SetIdentity("SecurityManager");
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SecurityManager_IssueCredential_Returns201()
    {
        SetIdentity("SecurityManager");
        using var client = _factory.CreateDefaultClient();
        var personId = await SeedActivePerson();
        var response = await client.PostAsJsonAsync($"/api/v1/people/{personId}/credentials", new
        { credentialNumber = $"SM-{Guid.NewGuid():N}"[..20], accessLevel = "Clinical", expiresAt = DateTimeOffset.UtcNow.AddDays(365).ToString("o") });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task SecurityManager_RevokeCredential_Returns200()
    {
        SetIdentity("SecurityManager");
        using var client = _factory.CreateDefaultClient();
        var credId = await SeedActiveCredential();
        var response = await client.PostAsJsonAsync($"/api/v1/credentials/{credId}/revoke", new { reason = "Lost" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // === CredentialAdministrator allowed ===

    [Theory]
    [InlineData("/api/v1/people")]
    [InlineData("/api/v1/credentials")]
    [InlineData("/api/v1/credentials/summary")]
    public async Task CredentialAdministrator_ReadEndpoints_Returns200(string path)
    {
        SetIdentity("CredentialAdministrator");
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CredentialAdministrator_IssueCredential_Returns201()
    {
        SetIdentity("CredentialAdministrator");
        using var client = _factory.CreateDefaultClient();
        var personId = await SeedActivePerson();
        var response = await client.PostAsJsonAsync($"/api/v1/people/{personId}/credentials", new
        { credentialNumber = $"CA-{Guid.NewGuid():N}"[..20], accessLevel = "General", expiresAt = DateTimeOffset.UtcNow.AddDays(365).ToString("o") });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CredentialAdministrator_RevokeCredential_Returns200()
    {
        SetIdentity("CredentialAdministrator");
        using var client = _factory.CreateDefaultClient();
        var credId = await SeedActiveCredential();
        var response = await client.PostAsJsonAsync($"/api/v1/credentials/{credId}/revoke", new { reason = "Admin revoke" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // === Technician denied from mutations ===

    [Fact]
    public async Task Technician_IssueCredential_Returns403()
    {
        SetIdentity("Technician");
        using var client = _factory.CreateDefaultClient();
        var personId = await SeedActivePerson();
        var response = await client.PostAsJsonAsync($"/api/v1/people/{personId}/credentials", new
        { credentialNumber = "TECH-DENY", accessLevel = "General", expiresAt = DateTimeOffset.UtcNow.AddDays(365).ToString("o") });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Technician_RevokeCredential_Returns403()
    {
        SetIdentity("Technician");
        using var client = _factory.CreateDefaultClient();
        var credId = await SeedActiveCredential();
        var response = await client.PostAsJsonAsync($"/api/v1/credentials/{credId}/revoke", new { reason = "denied" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // === Health remains anonymous ===

    [Fact]
    public async Task Health_Unauthenticated_Returns200()
    {
        ClearIdentity();
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // === HELPERS ===

    private async Task<Guid> SeedActivePerson()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CredentialDbContext>();
        var p = new Person { Id = Guid.NewGuid(), FirstName = "A", LastName = "T", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<Guid> SeedActiveCredential()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CredentialDbContext>();
        var p = new Person { Id = Guid.NewGuid(), FirstName = "C", LastName = "H", PersonType = PersonType.Employee, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.People.Add(p);
        var c = new Credential { Id = Guid.NewGuid(), PersonId = p.Id, CredentialNumber = $"A-{Guid.NewGuid():N}"[..20], AccessLevel = CredentialAccessLevel.Clinical, IssuedAt = DateTimeOffset.UtcNow.AddDays(-30), ExpiresAt = DateTimeOffset.UtcNow.AddDays(335), CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) };
        db.Credentials.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }
}
