using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vision.WorkOrderService.Domain;
using Vision.WorkOrderService.Infrastructure.Messaging;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Tests.Messaging;

public class IncidentCreatedConsumerTests : IDisposable
{
    private readonly WorkOrderDbContext _db;

    public IncidentCreatedConsumerTests()
    {
        var options = new DbContextOptionsBuilder<WorkOrderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new WorkOrderDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private IncidentCreatedHandler CreateHandler() =>
        new(_db, NullLogger<IncidentCreatedHandler>.Instance);

    private static IncidentCreatedV1 CreateValidEvent(Guid? eventId = null, Guid? incidentId = null) => new()
    {
        EventId = eventId ?? Guid.NewGuid(),
        EventType = IncidentCreatedV1.EventTypeName,
        OccurredAt = DateTimeOffset.UtcNow,
        CorrelationId = Guid.NewGuid().ToString("N"),
        Incident = new IncidentCreatedIncidentV1
        {
            Id = incidentId ?? Guid.NewGuid(),
            Title = "Camera offline",
            Description = "Camera stopped responding",
            Severity = "Critical"
        },
        Asset = new IncidentCreatedAssetV1
        {
            Id = Guid.NewGuid(),
            Name = "Pharmacy Camera 02",
            AssetTag = "CAM-002",
            AssetType = "Camera"
        },
        Location = new IncidentCreatedLocationV1
        {
            Id = Guid.NewGuid(),
            Name = "Pharmacy Storage",
            BuildingId = Guid.NewGuid(),
            BuildingName = "Main Hospital"
        }
    };

    [Fact]
    public async Task HandleAsync_ValidEvent_CreatesWorkOrder()
    {
        var handler = CreateHandler();
        var evt = CreateValidEvent();

        var result = await handler.HandleAsync(evt, CancellationToken.None);

        Assert.True(result);
        var wo = await _db.WorkOrders.FirstOrDefaultAsync();
        Assert.NotNull(wo);
        Assert.Equal(WorkOrderStatus.New, wo.Status);
        Assert.Equal(WorkOrderPriority.Critical, wo.Priority);
        Assert.Equal(evt.EventId, wo.SourceEventId);
        Assert.Equal(evt.Incident.Id, wo.SecurityIncidentId);
        Assert.Equal(evt.Asset.Id, wo.SecurityAssetId);
        Assert.Equal(evt.Asset.Name, wo.AssetNameSnapshot);
        Assert.Equal(evt.Location.Name, wo.LocationNameSnapshot);
        Assert.Equal(evt.CorrelationId, wo.CorrelationId);
        Assert.StartsWith("Repair: ", wo.Title);
    }

    [Fact]
    public async Task HandleAsync_SameEventIdTwice_CreatesOneWorkOrder()
    {
        var handler = CreateHandler();
        var eventId = Guid.NewGuid();
        var evt = CreateValidEvent(eventId: eventId);

        await handler.HandleAsync(evt, CancellationToken.None);
        var result = await handler.HandleAsync(evt, CancellationToken.None);

        Assert.True(result); // Acknowledged as idempotent success
        Assert.Equal(1, await _db.WorkOrders.CountAsync());
    }

    [Fact]
    public async Task HandleAsync_SameIncidentDifferentEventIds_CreatesOneWorkOrder()
    {
        var handler = CreateHandler();
        var incidentId = Guid.NewGuid();
        var evt1 = CreateValidEvent(incidentId: incidentId);
        var evt2 = CreateValidEvent(incidentId: incidentId);

        await handler.HandleAsync(evt1, CancellationToken.None);
        var result = await handler.HandleAsync(evt2, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(1, await _db.WorkOrders.CountAsync());
    }

    [Fact]
    public async Task HandleAsync_ManualWorkOrderExists_AcknowledgesWithoutDuplicate()
    {
        var incidentId = Guid.NewGuid();
        _db.WorkOrders.Add(new WorkOrder
        {
            Id = Guid.NewGuid(),
            SecurityAssetId = Guid.NewGuid(),
            SecurityIncidentId = incidentId,
            Title = "Manual WO",
            Description = "Created manually",
            Priority = WorkOrderPriority.High,
            Status = WorkOrderStatus.New,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();

        var handler = CreateHandler();
        var evt = CreateValidEvent(incidentId: incidentId);

        var result = await handler.HandleAsync(evt, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(1, await _db.WorkOrders.CountAsync());
    }

    [Fact]
    public async Task HandleAsync_NonCriticalSeverity_RejectsAsContractViolation()
    {
        var handler = CreateHandler();
        var evt = CreateValidEvent();
        var modifiedEvt = new IncidentCreatedV1
        {
            EventId = evt.EventId,
            EventType = evt.EventType,
            OccurredAt = evt.OccurredAt,
            CorrelationId = evt.CorrelationId,
            Incident = new IncidentCreatedIncidentV1
            {
                Id = evt.Incident.Id,
                Title = evt.Incident.Title,
                Description = evt.Incident.Description,
                Severity = "High" // Not Critical
            },
            Asset = evt.Asset,
            Location = evt.Location
        };

        var result = await handler.HandleAsync(modifiedEvt, CancellationToken.None);

        Assert.False(result); // Not acknowledged — DLQ eligible
        Assert.Equal(0, await _db.WorkOrders.CountAsync());
    }

    [Fact]
    public async Task HandleAsync_InvalidEvent_MissingEventId_RejectsAsContractViolation()
    {
        var handler = CreateHandler();
        var evt = CreateValidEvent();
        var modifiedEvt = new IncidentCreatedV1
        {
            EventId = Guid.Empty, // Invalid
            EventType = evt.EventType,
            OccurredAt = evt.OccurredAt,
            CorrelationId = evt.CorrelationId,
            Incident = evt.Incident,
            Asset = evt.Asset,
            Location = evt.Location
        };

        var result = await handler.HandleAsync(modifiedEvt, CancellationToken.None);

        Assert.False(result);
        Assert.Equal(0, await _db.WorkOrders.CountAsync());
    }

    [Fact]
    public async Task HandleAsync_WrongEventType_RejectsAsContractViolation()
    {
        var handler = CreateHandler();
        var evt = CreateValidEvent();
        var modifiedEvt = new IncidentCreatedV1
        {
            EventId = evt.EventId,
            EventType = "vision.security-operations.incident-created.v2", // Unsupported
            OccurredAt = evt.OccurredAt,
            CorrelationId = evt.CorrelationId,
            Incident = evt.Incident,
            Asset = evt.Asset,
            Location = evt.Location
        };

        var result = await handler.HandleAsync(modifiedEvt, CancellationToken.None);

        Assert.False(result);
        Assert.Equal(0, await _db.WorkOrders.CountAsync());
    }

    [Fact]
    public async Task HandleAsync_BlankCorrelationId_RejectsAsContractViolation()
    {
        var handler = CreateHandler();
        var evt = new IncidentCreatedV1
        {
            EventId = Guid.NewGuid(),
            EventType = IncidentCreatedV1.EventTypeName,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = "", // Invalid
            Incident = new IncidentCreatedIncidentV1
            {
                Id = Guid.NewGuid(),
                Title = "Test",
                Description = "Test",
                Severity = "Critical"
            },
            Asset = new IncidentCreatedAssetV1
            {
                Id = Guid.NewGuid(),
                Name = "Cam",
                AssetType = "Camera"
            },
            Location = new IncidentCreatedLocationV1
            {
                Id = Guid.NewGuid(),
                Name = "Loc",
                BuildingId = Guid.NewGuid(),
                BuildingName = "Bldg"
            }
        };

        var result = await handler.HandleAsync(evt, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task HandleAsync_CorrelationIdPreservedInWorkOrder()
    {
        var handler = CreateHandler();
        var evt = CreateValidEvent();

        await handler.HandleAsync(evt, CancellationToken.None);

        var wo = await _db.WorkOrders.FirstAsync();
        Assert.Equal(evt.CorrelationId, wo.CorrelationId);
        Assert.Equal(evt.EventId, wo.SourceEventId);
    }
}
