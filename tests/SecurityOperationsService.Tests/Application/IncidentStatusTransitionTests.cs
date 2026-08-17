using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vision.SecurityOperationsService.Domain;
using Vision.SecurityOperationsService.Infrastructure.Persistence;
using Vision.SecurityOperationsService.Infrastructure.Persistence.Seeding;

namespace Vision.SecurityOperationsService.Tests.Application;

/// <summary>
/// API-level coverage for SecurityManager incident-status transitions. Authorization
/// itself is covered in SecurityOperationsAuthorizationTests; these tests focus on
/// the previously-untested positive PATCH paths and invalid-transition safety.
/// </summary>
[Collection("SecurityOperationsApplication")]
public class IncidentStatusTransitionTests : IAsyncLifetime
{
    private readonly SecurityOperationsApplicationFactory _factory = new();

    public async Task InitializeAsync() => await _factory.EnsureDatabaseReadyAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PatchStatus_OpenToInvestigating_Returns200AndPersistsStatus()
    {
        var incidentId = await SeedIncidentAsync(IncidentStatus.Open, IncidentSeverity.Medium);
        using var client = _factory.CreateDefaultClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/incidents/{incidentId}",
            new { status = "Investigating", resolutionSummary = (string?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(IncidentStatus.Investigating, await GetStatusAsync(incidentId));
    }

    [Fact]
    public async Task PatchStatus_ValidResolution_Returns200SetsResolvedAtAndPersistsSummary()
    {
        var incidentId = await SeedIncidentAsync(IncidentStatus.Investigating, IncidentSeverity.Medium);
        using var client = _factory.CreateDefaultClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/incidents/{incidentId}",
            new { status = "Resolved", resolutionSummary = "Camera replaced and verified operational." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();
        var incident = await db.SecurityIncidents.AsNoTracking().FirstAsync(i => i.Id == incidentId);

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal("Camera replaced and verified operational.", incident.ResolutionSummary);
        Assert.True(incident.ResolvedAt.HasValue);
    }

    [Fact]
    public async Task PatchStatus_ResolvedToInvestigating_FailsAndLeavesStateUnchanged()
    {
        var incidentId = await SeedIncidentAsync(
            IncidentStatus.Resolved, IncidentSeverity.Medium, resolutionSummary: "Already handled.");

        using var client = _factory.CreateDefaultClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/incidents/{incidentId}",
            new { status = "Investigating", resolutionSummary = (string?)null });

        // Established exception mapping: InvalidOperationException -> 409 Conflict.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();
        var incident = await db.SecurityIncidents.AsNoTracking().FirstAsync(i => i.Id == incidentId);

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal("Already handled.", incident.ResolutionSummary);
    }

    // === Helpers ===

    private async Task<Guid> SeedIncidentAsync(
        IncidentStatus status, IncidentSeverity severity, string? resolutionSummary = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();

        var now = DateTimeOffset.UtcNow.AddHours(-1);
        var incident = new SecurityIncident
        {
            Id = Guid.NewGuid(),
            LocationId = SeedDataIds.PharmacyStorage,
            SecurityAssetId = SeedDataIds.PharmacyStorageCamera02,
            Title = $"Status transition test {Guid.NewGuid():N}"[..40],
            Description = "Seeded for status-transition testing.",
            Severity = severity,
            Status = status,
            ResolutionSummary = resolutionSummary,
            ResolvedAt = status == IncidentStatus.Resolved ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.SecurityIncidents.Add(incident);
        await db.SaveChangesAsync();
        return incident.Id;
    }

    private async Task<IncidentStatus> GetStatusAsync(Guid incidentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();
        return await db.SecurityIncidents
            .AsNoTracking()
            .Where(i => i.Id == incidentId)
            .Select(i => i.Status)
            .FirstAsync();
    }
}
