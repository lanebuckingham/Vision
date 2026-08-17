using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vision.SecurityOperationsService.Application.Common;
using Vision.SecurityOperationsService.Application.Incidents.Commands;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Tests.Integration;

/// <summary>
/// Verifies the Phase 6 observability requirement that W3C trace context is captured
/// on the outbox row when a qualifying incident is created under an active Activity,
/// and that the existing Vision CorrelationId keeps working independently of whether
/// a trace context exists. See docs/development/observability.md.
/// Requires: docker compose up -d (PostgreSQL on localhost:5432)
/// </summary>
[Collection("PostgreSQL")]
public class OutboxTraceContextTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Database=vision_test_outbox_trace;Username=vision;Password=vision_dev";

    private const string TestSourceName = "Vision.Tests.OutboxTraceContext";
    private static readonly ActivitySource TestSource = new(TestSourceName);

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
        await SecurityOperationsService.Infrastructure.Persistence.Seeding.SecurityOperationsSeeder.SeedAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        _db.Dispose();
    }

    private async Task<(Guid IncidentId, Guid AssetId, Guid LocationId)> GetSeededAssetAsync()
    {
        var assetId = SecurityOperationsService.Infrastructure.Persistence.Seeding.SeedDataIds.PharmacyStorageCamera02;
        var asset = await _db.SecurityAssets.AsNoTracking().FirstAsync(a => a.Id == assetId);
        return (Guid.Empty, assetId, asset.LocationId);
    }

    [Fact]
    public async Task QualifyingIncident_WithActiveActivity_PersistsTraceParentOnOutbox()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TestSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = TestSource.StartActivity("test-incident-creation");
        Assert.NotNull(activity); // Sanity check the listener actually attached

        var (_, assetId, locationId) = await GetSeededAssetAsync();
        var correlationCtx = new CorrelationContext { CorrelationId = "trace-persist-test" };
        var handler = new CreateIncidentCommandHandler(_db, correlationCtx, NullLogger<CreateIncidentCommandHandler>.Instance);

        var command = new CreateIncidentCommand(
            LocationId: locationId,
            AssetId: assetId,
            Title: "Trace persistence test incident",
            Description: "Verifying TraceParent is captured on the outbox row",
            Severity: "Critical");

        await handler.Handle(command, CancellationToken.None);

        var outbox = await _db.OutboxMessages.FirstAsync();
        Assert.False(string.IsNullOrWhiteSpace(outbox.TraceParent));
        Assert.Equal(activity!.Id, outbox.TraceParent);

        // The durable Vision CorrelationId must remain independently available.
        Assert.Equal("trace-persist-test", outbox.CorrelationId);
    }

    [Fact]
    public async Task QualifyingIncident_WithoutActiveActivity_SucceedsWithNullTraceContext()
    {
        // Explicitly ensure no ambient Activity exists for this test.
        Assert.Null(Activity.Current);

        var (_, assetId, locationId) = await GetSeededAssetAsync();
        var correlationCtx = new CorrelationContext { CorrelationId = "no-trace-context-test" };
        var handler = new CreateIncidentCommandHandler(_db, correlationCtx, NullLogger<CreateIncidentCommandHandler>.Instance);

        var command = new CreateIncidentCommand(
            LocationId: locationId,
            AssetId: assetId,
            Title: "No-activity fallback test incident",
            Description: "Verifying outbox creation succeeds without a current Activity",
            Severity: "Critical");

        // Must not throw — absence of trace context must never block the business transaction.
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        var outbox = await _db.OutboxMessages.FirstAsync();
        Assert.Null(outbox.TraceParent);
        Assert.Null(outbox.TraceState);

        // CorrelationId is unaffected by the absence of trace context.
        Assert.Equal("no-trace-context-test", outbox.CorrelationId);
    }
}
