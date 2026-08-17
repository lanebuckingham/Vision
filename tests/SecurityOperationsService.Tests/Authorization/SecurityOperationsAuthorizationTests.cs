using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vision.SecurityOperationsService.Domain;
using Vision.SecurityOperationsService.Infrastructure.Persistence;
using Vision.SecurityOperationsService.Infrastructure.Persistence.Seeding;

namespace Vision.SecurityOperationsService.Tests.Authorization;

/// <summary>
/// Authorization integration tests for SecurityOperationsService.
///
/// Approved matrix: every business endpoint requires the SecurityOperationsManager
/// policy (SecurityManager group only). Technician and CredentialAdministrator are denied.
/// </summary>
public class SecurityOperationsAuthorizationTests : IAsyncLifetime
{
    private readonly SecurityOperationsAuthFactory _factory = new();

    private static readonly string[] ReadEndpoints =
    [
        "/api/v1/dashboard",
        "/api/v1/assets",
        "/api/v1/incidents",
    ];

    public async Task InitializeAsync() => await _factory.EnsureDatabaseReady();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // === 401 — Unauthenticated ===

    [Theory]
    [InlineData("/api/v1/dashboard")]
    [InlineData("/api/v1/assets")]
    [InlineData("/api/v1/incidents")]
    public async Task Unauthenticated_ProtectedEndpoint_Returns401(string path)
    {
        _factory.ClearIdentity();
        using var client = _factory.CreateDefaultClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_AssetStatusMutation_Returns401()
    {
        _factory.ClearIdentity();
        using var client = _factory.CreateDefaultClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/v1/assets/{SeedDataIds.PharmacyStorageCamera02}/status",
            new { status = "Operational" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Health_Returns200()
    {
        _factory.ClearIdentity();
        using var client = _factory.CreateDefaultClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    // === SecurityManager allowed ===

    [Theory]
    [InlineData("/api/v1/dashboard")]
    [InlineData("/api/v1/assets")]
    [InlineData("/api/v1/incidents")]
    public async Task SecurityManager_ReadEndpoints_Returns200(string path)
    {
        _factory.SetIdentity("SecurityManager");
        using var client = _factory.CreateDefaultClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task SecurityManager_AssetDetail_Returns200()
    {
        _factory.SetIdentity("SecurityManager");
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync($"/api/v1/assets/{SeedDataIds.PharmacyStorageCamera02}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SecurityManager_IncidentDetail_Returns200()
    {
        _factory.SetIdentity("SecurityManager");
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync($"/api/v1/incidents/{SeedDataIds.PharmacyCameraIncident}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SecurityManager_UpdateAssetStatus_Succeeds()
    {
        _factory.SetIdentity("SecurityManager");
        using var client = _factory.CreateDefaultClient();

        var assetId = await SeedIsolatedAssetAsync(SecurityAssetStatus.Offline);

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/assets/{assetId}/status",
            new { status = "Operational" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SecurityAssetStatus.Operational, await GetAssetStatusAsync(assetId));
    }

    [Fact]
    public async Task SecurityManager_CreateIncident_Returns201()
    {
        _factory.SetIdentity("SecurityManager");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync("/api/v1/incidents", new
        {
            locationId = SeedDataIds.PharmacyStorage,
            assetId = SeedDataIds.PharmacyStorageCamera02,
            title = "Authorization test incident",
            description = "Created by SecurityManager authorization test.",
            severity = "Low",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task SecurityManager_InvalidRequest_Returns400NotAuthorizationError()
    {
        // Authorization must not swallow normal business validation for allowed roles.
        _factory.SetIdentity("SecurityManager");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/assets/{SeedDataIds.PharmacyStorageCamera02}/status",
            new { status = "NotARealStatus" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // === Technician denied ===

    [Theory]
    [InlineData("/api/v1/dashboard")]
    [InlineData("/api/v1/assets")]
    [InlineData("/api/v1/incidents")]
    public async Task Technician_ReadEndpoints_Returns403(string path)
    {
        _factory.SetIdentity("Technician");
        using var client = _factory.CreateDefaultClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Technician_AssetDetail_Returns403()
    {
        _factory.SetIdentity("Technician");
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync($"/api/v1/assets/{SeedDataIds.PharmacyStorageCamera02}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Technician_IncidentDetail_Returns403()
    {
        _factory.SetIdentity("Technician");
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync($"/api/v1/incidents/{SeedDataIds.PharmacyCameraIncident}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Technician_UpdateAssetStatus_Returns403AndDoesNotMutate()
    {
        var assetId = await SeedIsolatedAssetAsync(SecurityAssetStatus.Offline);

        _factory.SetIdentity("Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/assets/{assetId}/status",
            new { status = "Operational" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(SecurityAssetStatus.Offline, await GetAssetStatusAsync(assetId));
    }

    [Fact]
    public async Task Technician_CreateIncident_Returns403AndDoesNotMutate()
    {
        var countBefore = await CountIncidentsAsync();

        _factory.SetIdentity("Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync("/api/v1/incidents", new
        {
            locationId = SeedDataIds.PharmacyStorage,
            assetId = SeedDataIds.PharmacyStorageCamera02,
            title = "Technician should not create this",
            description = "Denied incident creation attempt.",
            severity = "Critical",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(countBefore, await CountIncidentsAsync());
    }

    [Fact]
    public async Task Technician_UpdateIncidentStatus_Returns403()
    {
        _factory.SetIdentity("Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/incidents/{SeedDataIds.PharmacyCameraIncident}",
            new { status = "Resolved", resolutionSummary = "Denied attempt" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // === CredentialAdministrator denied ===

    [Theory]
    [InlineData("/api/v1/dashboard")]
    [InlineData("/api/v1/assets")]
    [InlineData("/api/v1/incidents")]
    public async Task CredentialAdministrator_ReadEndpoints_Returns403(string path)
    {
        _factory.SetIdentity("CredentialAdministrator");
        using var client = _factory.CreateDefaultClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task CredentialAdministrator_UpdateAssetStatus_Returns403()
    {
        _factory.SetIdentity("CredentialAdministrator");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/assets/{SeedDataIds.PharmacyStorageCamera02}/status",
            new { status = "Operational" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // === No approved Vision role ===

    [Fact]
    public async Task AuthenticatedWithNoApprovedRole_Returns403()
    {
        _factory.SetIdentity("SomeUnrelatedGroup");
        using var client = _factory.CreateDefaultClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/dashboard")).StatusCode);
    }

    // === Helpers ===

    private async Task<Guid> SeedIsolatedAssetAsync(SecurityAssetStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();

        var asset = new SecurityAsset
        {
            Id = Guid.NewGuid(),
            LocationId = SeedDataIds.PharmacyStorage,
            Name = $"Auth Test Asset {Guid.NewGuid():N}"[..30],
            AssetType = SecurityAssetType.Camera,
            Status = status,
            StatusChangedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

        db.SecurityAssets.Add(asset);
        await db.SaveChangesAsync();
        return asset.Id;
    }

    private async Task<SecurityAssetStatus> GetAssetStatusAsync(Guid assetId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();
        return await db.SecurityAssets
            .AsNoTracking()
            .Where(a => a.Id == assetId)
            .Select(a => a.Status)
            .FirstAsync();
    }

    private async Task<int> CountIncidentsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();
        return await db.SecurityIncidents.AsNoTracking().CountAsync();
    }
}
