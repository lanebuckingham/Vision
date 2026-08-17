using System.Net;
using System.Net.Http.Json;

namespace Vision.SecurityOperationsService.Tests.Application;

/// <summary>
/// Coverage for the Phase 6 health-check endpoints. Verifies the semantic difference
/// between "process alive" and "ready to serve work" rather than asserting exact
/// framework-generated JSON.
/// </summary>
[Collection("SecurityOperationsApplication")]
public class HealthCheckTests : IAsyncLifetime
{
    private readonly SecurityOperationsApplicationFactory _factory = new();

    public async Task InitializeAsync() => await _factory.EnsureDatabaseReadyAsync();

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
        // Liveness must not depend on PostgreSQL — verify it doesn't even report on it,
        // so a temporary DB outage cannot make the process look dead.
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
        Assert.Contains("SecurityOperationsService", body);
    }

    [Fact]
    public async Task HealthResponses_DoNotExposeConnectionStringsOrSecrets()
    {
        using var client = _factory.CreateDefaultClient();

        var liveBody = await (await client.GetAsync("/health/live")).Content.ReadAsStringAsync();
        var readyBody = await (await client.GetAsync("/health/ready")).Content.ReadAsStringAsync();

        foreach (var body in new[] { liveBody, readyBody })
        {
            Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Host=", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record HealthResponse(string Status, List<HealthCheckEntry> Checks);
    private sealed record HealthCheckEntry(string Name, string Status);
}
