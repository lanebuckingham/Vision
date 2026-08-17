using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vision.SecurityOperationsService.Application.Assets.Queries;
using Vision.SecurityOperationsService.Application.Dashboard.Queries;
using Vision.SecurityOperationsService.Application.Incidents.Queries;
using Vision.SecurityOperationsService.Domain;
using Vision.SecurityOperationsService.Infrastructure.Persistence;
using Vision.SecurityOperationsService.Infrastructure.Persistence.Seeding;

namespace Vision.SecurityOperationsService.Tests.Application;

/// <summary>
/// Behavioral coverage for the security dashboard — the opening screen of the
/// five-minute demo. Rather than asserting exact totals (the database already
/// carries demo seed data), these tests verify internal consistency and use
/// before/after deltas around freshly-seeded fixture rows so results remain
/// deterministic regardless of what else is in the database.
/// </summary>
[Collection("SecurityOperationsApplication")]
public class SecurityDashboardTests : IAsyncLifetime
{
    private readonly SecurityOperationsApplicationFactory _factory = new();

    public async Task InitializeAsync() => await _factory.EnsureDatabaseReadyAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Dashboard_AssetCounts_AreInternallyConsistent()
    {
        using var client = _factory.CreateDefaultClient();
        var dashboard = await client.GetFromJsonAsync<SecurityDashboardDto>("/api/v1/dashboard");

        Assert.NotNull(dashboard);
        var health = dashboard!.SecurityHealth;

        Assert.Equal(health.TotalAssets, health.OperationalAssets + health.DegradedAssets + health.OfflineAssets);

        var expectedPercentage = health.TotalAssets > 0
            ? (int)Math.Round(100.0 * health.OperationalAssets / health.TotalAssets)
            : 0;
        Assert.Equal(expectedPercentage, health.OperationalPercentage);
    }

    [Fact]
    public async Task Dashboard_ResolvedIncidents_AreExcludedFromActiveCounts()
    {
        using var client = _factory.CreateDefaultClient();

        var totalIncidentsBefore = await CountAllIncidentsAsync();
        var resolvedIncidentsBefore = await CountIncidentsAsync(i => i.Status == IncidentStatus.Resolved);

        var dashboard = await client.GetFromJsonAsync<SecurityDashboardDto>("/api/v1/dashboard");

        Assert.NotNull(dashboard);
        Assert.Equal(totalIncidentsBefore - resolvedIncidentsBefore, dashboard!.Incidents.ActiveTotal);
    }

    [Fact]
    public async Task Dashboard_NewActiveCriticalIncident_IncreasesActiveCriticalCount()
    {
        using var client = _factory.CreateDefaultClient();

        var before = await client.GetFromJsonAsync<SecurityDashboardDto>("/api/v1/dashboard");
        Assert.NotNull(before);

        await SeedCriticalIncidentAsync(DateTimeOffset.UtcNow, IncidentStatus.Open);

        var after = await client.GetFromJsonAsync<SecurityDashboardDto>("/api/v1/dashboard");
        Assert.NotNull(after);

        Assert.Equal(before!.Incidents.ActiveCritical + 1, after!.Incidents.ActiveCritical);
    }

    [Fact]
    public async Task Dashboard_CriticalAlerts_ContainOnlyActiveCriticalIncidents()
    {
        using var client = _factory.CreateDefaultClient();
        var dashboard = await client.GetFromJsonAsync<SecurityDashboardDto>("/api/v1/dashboard");

        Assert.NotNull(dashboard);
        Assert.All(dashboard!.CriticalAlerts, alert =>
        {
            Assert.Equal(nameof(IncidentSeverity.Critical), alert.Severity);
            Assert.NotEqual(nameof(IncidentStatus.Resolved), alert.Status);
        });
    }

    [Fact]
    public async Task Dashboard_CriticalAlerts_AreNewestFirstAndCappedAtFive()
    {
        // Seed 6 new active Critical incidents with strictly increasing CreatedAt, anchored
        // far in the future so no other seeded/sibling-test incident can outrank them
        // regardless of test execution order or leftover data from previous runs.
        var farFuture = DateTimeOffset.UtcNow.AddYears(1);
        var newestId = Guid.Empty;
        for (var i = 0; i < 6; i++)
        {
            var createdAt = farFuture.AddMinutes(i);
            var id = await SeedCriticalIncidentAsync(createdAt, IncidentStatus.Open);
            if (i == 5) newestId = id;
        }

        using var client = _factory.CreateDefaultClient();
        var dashboard = await client.GetFromJsonAsync<SecurityDashboardDto>("/api/v1/dashboard");

        Assert.NotNull(dashboard);
        Assert.True(dashboard!.CriticalAlerts.Count <= 5);
        Assert.Equal(newestId, dashboard.CriticalAlerts[0].IncidentId);

        // Newest-first ordering
        for (var i = 1; i < dashboard.CriticalAlerts.Count; i++)
        {
            Assert.True(dashboard.CriticalAlerts[i - 1].CreatedAt >= dashboard.CriticalAlerts[i].CreatedAt);
        }
    }

    [Fact]
    public async Task Dashboard_RecentActivity_UsesTruthfulCreationAndResolutionTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        var createdIncidentId = await SeedCriticalIncidentAsync(now.AddMinutes(10), IncidentStatus.Open);
        var resolvedIncidentId = await SeedResolvedIncidentAsync(
            createdAt: now.AddDays(-5), resolvedAt: now.AddMinutes(9));

        using var client = _factory.CreateDefaultClient();
        var dashboard = await client.GetFromJsonAsync<SecurityDashboardDto>("/api/v1/dashboard");

        Assert.NotNull(dashboard);

        var createdActivity = dashboard!.RecentActivity
            .FirstOrDefault(a => a.IncidentId == createdIncidentId && a.Type == "IncidentCreated");
        Assert.NotNull(createdActivity);
        Assert.Equal(now.AddMinutes(10), createdActivity!.OccurredAt);

        var resolvedActivity = dashboard.RecentActivity
            .FirstOrDefault(a => a.IncidentId == resolvedIncidentId && a.Type == "IncidentResolved");
        Assert.NotNull(resolvedActivity);
        Assert.Equal(now.AddMinutes(9), resolvedActivity!.OccurredAt);
    }

    // === Query smoke tests ===

    [Fact]
    public async Task GetAssets_StatusFilter_ReturnsOnlyMatchingAssets()
    {
        using var client = _factory.CreateDefaultClient();
        var result = await client.GetFromJsonAsync<PagedAssetsResponse>("/api/v1/assets?status=Offline");

        Assert.NotNull(result);
        Assert.All(result!.Items, a => Assert.Equal("Offline", a.Status));
    }

    [Fact]
    public async Task GetAssets_TypeFilter_ReturnsOnlyMatchingAssets()
    {
        using var client = _factory.CreateDefaultClient();
        var result = await client.GetFromJsonAsync<PagedAssetsResponse>("/api/v1/assets?type=Camera");

        Assert.NotNull(result);
        Assert.All(result!.Items, a => Assert.Equal("Camera", a.AssetType));
        Assert.NotEmpty(result.Items);
    }

    [Fact]
    public async Task GetAssets_Search_FindsSeededAssetByName()
    {
        using var client = _factory.CreateDefaultClient();
        var result = await client.GetFromJsonAsync<PagedAssetsResponse>("/api/v1/assets?search=Pharmacy Storage Camera 02");

        Assert.NotNull(result);
        Assert.Contains(result!.Items, a => a.Id == SeedDataIds.PharmacyStorageCamera02);
    }

    [Fact]
    public async Task GetAssets_Pagination_ReturnsRequestedPageSize()
    {
        using var client = _factory.CreateDefaultClient();
        var result = await client.GetFromJsonAsync<PagedAssetsResponse>("/api/v1/assets?page=1&pageSize=2");

        Assert.NotNull(result);
        Assert.True(result!.Items.Count <= 2);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task GetIncidents_StatusFilter_ReturnsOnlyMatchingIncidents()
    {
        using var client = _factory.CreateDefaultClient();
        var result = await client.GetFromJsonAsync<PagedIncidentsResponse>("/api/v1/incidents?status=Resolved");

        Assert.NotNull(result);
        Assert.All(result!.Items, i => Assert.Equal("Resolved", i.Status));
    }

    [Fact]
    public async Task GetIncidents_SeverityFilter_ReturnsOnlyMatchingIncidents()
    {
        using var client = _factory.CreateDefaultClient();
        var result = await client.GetFromJsonAsync<PagedIncidentsResponse>("/api/v1/incidents?severity=Critical");

        Assert.NotNull(result);
        Assert.All(result!.Items, i => Assert.Equal("Critical", i.Severity));
    }

    [Fact]
    public async Task GetIncidents_AssetFilter_ReturnsOnlyIncidentsForThatAsset()
    {
        using var client = _factory.CreateDefaultClient();
        var result = await client.GetFromJsonAsync<PagedIncidentsResponse>(
            $"/api/v1/incidents?assetId={SeedDataIds.PharmacyStorageCamera02}");

        Assert.NotNull(result);
        Assert.NotEmpty(result!.Items);
        Assert.All(result.Items, i => Assert.Equal(SeedDataIds.PharmacyStorageCamera02, i.Asset?.Id));
    }

    [Fact]
    public async Task GetIncidents_Search_FindsSeededIncidentByTitle()
    {
        using var client = _factory.CreateDefaultClient();
        var result = await client.GetFromJsonAsync<PagedIncidentsResponse>("/api/v1/incidents?search=pharmacy storage camera offline");

        Assert.NotNull(result);
        Assert.Contains(result!.Items, i => i.Id == SeedDataIds.PharmacyCameraIncident);
    }

    [Fact]
    public async Task GetIncidents_Pagination_ReturnsRequestedPageSize()
    {
        using var client = _factory.CreateDefaultClient();
        var result = await client.GetFromJsonAsync<PagedIncidentsResponse>("/api/v1/incidents?page=1&pageSize=2");

        Assert.NotNull(result);
        Assert.True(result!.Items.Count <= 2);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    // === Helpers ===

    private async Task<Guid> SeedCriticalIncidentAsync(DateTimeOffset createdAt, IncidentStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();

        var incident = new SecurityIncident
        {
            Id = Guid.NewGuid(),
            LocationId = SeedDataIds.PharmacyStorage,
            SecurityAssetId = SeedDataIds.PharmacyStorageCamera02,
            Title = $"Dashboard test critical incident {Guid.NewGuid():N}"[..50],
            Description = "Seeded for dashboard calculation testing.",
            Severity = IncidentSeverity.Critical,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        db.SecurityIncidents.Add(incident);
        await db.SaveChangesAsync();
        return incident.Id;
    }

    private async Task<Guid> SeedResolvedIncidentAsync(DateTimeOffset createdAt, DateTimeOffset resolvedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();

        var incident = new SecurityIncident
        {
            Id = Guid.NewGuid(),
            LocationId = SeedDataIds.PharmacyStorage,
            Title = $"Dashboard test resolved incident {Guid.NewGuid():N}"[..50],
            Description = "Seeded for dashboard recent-activity testing.",
            Severity = IncidentSeverity.Low,
            Status = IncidentStatus.Resolved,
            ResolutionSummary = "Resolved for test purposes.",
            CreatedAt = createdAt,
            ResolvedAt = resolvedAt,
            UpdatedAt = resolvedAt
        };

        db.SecurityIncidents.Add(incident);
        await db.SaveChangesAsync();
        return incident.Id;
    }

    private async Task<int> CountAllIncidentsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();
        return await db.SecurityIncidents.AsNoTracking().CountAsync();
    }

    private async Task<int> CountIncidentsAsync(System.Linq.Expressions.Expression<Func<SecurityIncident, bool>> predicate)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();
        return await db.SecurityIncidents.AsNoTracking().CountAsync(predicate);
    }

    // Local response shapes mirroring PagedList<T> as returned over HTTP.
    private sealed record PagedAssetsResponse(
        List<AssetListItemDto> Items, int Page, int PageSize, int TotalCount);

    private sealed record PagedIncidentsResponse(
        List<IncidentListItemDto> Items, int Page, int PageSize, int TotalCount);
}
