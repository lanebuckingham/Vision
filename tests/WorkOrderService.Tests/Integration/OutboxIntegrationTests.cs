using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vision.SecurityOperationsService.Application.Common;
using Vision.SecurityOperationsService.Application.Incidents.Commands;
using Vision.SecurityOperationsService.Infrastructure.Messaging;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Tests.Integration;

/// <summary>
/// Tests the transactional outbox behavior in SecurityOperationsService.
/// Requires: docker compose up -d (vision-postgres on localhost:5432)
/// </summary>
[Collection("PostgreSQL")]
public class OutboxIntegrationTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Database=vision_test_outbox;Username=vision;Password=vision_dev";

    private SecurityOperationsDbContext _db = null!;

    private SecurityOperationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SecurityOperationsDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new SecurityOperationsDbContext(options);
    }

    public async Task InitializeAsync()
    {
        _db = CreateContext();
        await _db.Database.EnsureDeletedAsync();
        await _db.Database.EnsureCreatedAsync();

        // Seed minimal data for incident creation
        await SecurityOperationsService.Infrastructure.Persistence.Seeding.SecurityOperationsSeeder.SeedAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        _db.Dispose();
    }

    [Fact]
    public async Task OUTBOX_001_CriticalWithAsset_CreatesIncidentAndOutboxAtomically()
    {
        var correlationCtx = new CorrelationContext { CorrelationId = "test-correlation-001" };
        var handler = new CreateIncidentCommandHandler(_db, correlationCtx,
            NullLogger<CreateIncidentCommandHandler>.Instance);

        // Use seeded Pharmacy Storage Camera 02 + its location
        var pharmacyCameraId = SecurityOperationsService.Infrastructure.Persistence.Seeding.SeedDataIds.PharmacyStorageCamera02;
        var asset = await _db.SecurityAssets
            .AsNoTracking()
            .FirstAsync(a => a.Id == pharmacyCameraId);

        var command = new CreateIncidentCommand(
            LocationId: asset.LocationId,
            AssetId: pharmacyCameraId,
            Title: "Test critical incident",
            Description: "Camera offline for testing",
            Severity: "Critical");

        var result = await handler.Handle(command, CancellationToken.None);

        // Verify incident created
        var incident = await _db.SecurityIncidents.FirstAsync(i => i.Id == result.Id);
        Assert.NotNull(incident);

        // Verify outbox created in same transaction
        var outbox = await _db.OutboxMessages.FirstOrDefaultAsync();
        Assert.NotNull(outbox);
        Assert.Equal(IncidentCreatedV1.EventTypeName, outbox.EventType);
        Assert.Equal("test-correlation-001", outbox.CorrelationId);
        Assert.Null(outbox.PublishedAt);

        // Verify event payload
        var evt = JsonSerializer.Deserialize<IncidentCreatedV1>(outbox.Payload);
        Assert.NotNull(evt);
        Assert.Equal(incident.Id, evt.Incident.Id);
        Assert.Equal(pharmacyCameraId, evt.Asset.Id);
    }

    [Fact]
    public async Task OUTBOX_002_HighSeverityWithAsset_NoOutboxMessage()
    {
        var correlationCtx = new CorrelationContext { CorrelationId = "test-high-002" };
        var handler = new CreateIncidentCommandHandler(_db, correlationCtx,
            NullLogger<CreateIncidentCommandHandler>.Instance);

        var pharmacyCameraId = SecurityOperationsService.Infrastructure.Persistence.Seeding.SeedDataIds.PharmacyStorageCamera02;
        var asset = await _db.SecurityAssets.AsNoTracking().FirstAsync(a => a.Id == pharmacyCameraId);

        var command = new CreateIncidentCommand(
            LocationId: asset.LocationId,
            AssetId: pharmacyCameraId,
            Title: "High severity incident",
            Description: "Not critical",
            Severity: "High");

        await handler.Handle(command, CancellationToken.None);

        var outboxCount = await _db.OutboxMessages.CountAsync();
        Assert.Equal(0, outboxCount);
    }

    [Fact]
    public async Task OUTBOX_002b_CriticalWithoutAsset_NoOutboxMessage()
    {
        var correlationCtx = new CorrelationContext { CorrelationId = "test-noasset-002b" };
        var handler = new CreateIncidentCommandHandler(_db, correlationCtx,
            NullLogger<CreateIncidentCommandHandler>.Instance);

        // Get a valid location
        var location = await _db.Locations.AsNoTracking().FirstAsync();

        var command = new CreateIncidentCommand(
            LocationId: location.Id,
            AssetId: null, // No asset
            Title: "Critical without asset",
            Description: "No asset attached",
            Severity: "Critical");

        await handler.Handle(command, CancellationToken.None);

        var outboxCount = await _db.OutboxMessages.CountAsync();
        Assert.Equal(0, outboxCount);
    }

    [Fact]
    public async Task OUTBOX_003_004_EventIdStableAndPublishedAtTracked()
    {
        // Create a qualifying incident first
        var correlationCtx = new CorrelationContext { CorrelationId = "test-publish-003" };
        var handler = new CreateIncidentCommandHandler(_db, correlationCtx,
            NullLogger<CreateIncidentCommandHandler>.Instance);

        var pharmacyCameraId = SecurityOperationsService.Infrastructure.Persistence.Seeding.SeedDataIds.PharmacyStorageCamera02;
        var asset = await _db.SecurityAssets.AsNoTracking().FirstAsync(a => a.Id == pharmacyCameraId);

        var command = new CreateIncidentCommand(
            LocationId: asset.LocationId,
            AssetId: pharmacyCameraId,
            Title: "Publish test incident",
            Description: "Testing publisher behavior",
            Severity: "Critical");

        await handler.Handle(command, CancellationToken.None);

        var outbox = await _db.OutboxMessages.FirstAsync();
        var originalEventId = outbox.Id;
        Assert.Null(outbox.PublishedAt);
        Assert.Equal(0, outbox.AttemptCount);

        // Simulate failed publication attempt
        outbox.AttemptCount++;
        outbox.LastError = "Simulated send failure";
        await _db.SaveChangesAsync();

        // Verify EventId unchanged after failure
        var afterFailure = await _db.OutboxMessages.FirstAsync();
        Assert.Equal(originalEventId, afterFailure.Id);
        Assert.Null(afterFailure.PublishedAt);
        Assert.Equal(1, afterFailure.AttemptCount);
        Assert.NotNull(afterFailure.LastError);

        // Simulate successful publication
        afterFailure.PublishedAt = DateTimeOffset.UtcNow;
        afterFailure.LastError = null;
        await _db.SaveChangesAsync();

        var afterSuccess = await _db.OutboxMessages.FirstAsync();
        Assert.Equal(originalEventId, afterSuccess.Id); // EventId stable
        Assert.NotNull(afterSuccess.PublishedAt);
    }
}
