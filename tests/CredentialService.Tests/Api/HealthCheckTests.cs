using System.Net;
using System.Net.Http.Json;

namespace Vision.CredentialService.Tests.Api;

/// <summary>
/// Coverage for the Phase 6 health-check endpoints. Verifies the semantic difference
/// between "process alive" and "ready to serve work" rather than asserting exact
/// framework-generated JSON.
/// </summary>
public class HealthCheckTests : IAsyncLifetime
{
    private readonly CredentialServiceFactory _factory = new();

    public async Task InitializeAsync() => await _factory.EnsureDatabaseReady();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Live_ReturnsHealthyWithoutAuthentication()
    {
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("Healthy", body!.Status);
    }

    [Fact]
    public async Task Ready_ReturnsHealthyWhenDatabaseIsAvailable()
    {
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("Healthy", body!.Status);
        Assert.Contains(body.Checks, c => c.Name == "database" && c.Status == "Healthy");
    }

    [Fact]
    public async Task Live_DoesNotIncludeDatabaseCheck()
    {
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync("/health/live");

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.DoesNotContain(body!.Checks, c => c.Name == "database");
    }

    [Fact]
    public async Task CompatibilityAlias_StillReturnsHealthyServiceIdentity()
    {
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("CredentialService", body);
    }

    private sealed record HealthResponse(string Status, List<HealthCheckEntry> Checks);
    private sealed record HealthCheckEntry(string Name, string Status);
}
