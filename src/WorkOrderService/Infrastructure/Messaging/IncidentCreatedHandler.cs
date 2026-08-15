using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vision.WorkOrderService.Domain;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Infrastructure.Messaging;

/// <summary>
/// Handles IncidentCreated.v1 events by creating a WorkOrder idempotently.
/// Uses SourceEventId and SecurityIncidentId unique constraints for deduplication.
/// </summary>
public sealed class IncidentCreatedHandler(
    WorkOrderDbContext db,
    ILogger<IncidentCreatedHandler> logger)
{
    /// <summary>
    /// Processes a validated IncidentCreated event. Returns true if the message should be acknowledged.
    /// </summary>
    public async Task<bool> HandleAsync(IncidentCreatedV1 evt, CancellationToken cancellationToken)
    {
        // Validate contract
        if (!IsValid(evt))
        {
            logger.LogWarning("Rejected invalid event {EventId}: failed contract validation", evt.EventId);
            return false; // Permanent failure — let DLQ handle it
        }

        // Reject non-Critical events
        if (!string.Equals(evt.Incident.Severity, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Rejected event {EventId}: severity '{Severity}' is not Critical",
                evt.EventId, evt.Incident.Severity);
            return false; // Contract violation — DLQ
        }

        // Check idempotency: same EventId already processed?
        var existingByEvent = await db.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.SourceEventId == evt.EventId, cancellationToken);

        if (existingByEvent != null)
        {
            logger.LogInformation(
                "Duplicate event {EventId} already handled by WorkOrder {WorkOrderId}",
                evt.EventId, existingByEvent.Id);
            return true; // Idempotent success
        }

        // Check idempotency: same IncidentId already has a WorkOrder?
        var existingByIncident = await db.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.SecurityIncidentId == evt.Incident.Id, cancellationToken);

        if (existingByIncident != null)
        {
            logger.LogInformation(
                "Incident {IncidentId} already has WorkOrder {WorkOrderId}",
                evt.Incident.Id, existingByIncident.Id);
            return true; // Idempotent success
        }

        // Create WorkOrder
        var now = DateTimeOffset.UtcNow;
        var workOrder = new WorkOrder
        {
            Id = Guid.NewGuid(),
            SecurityAssetId = evt.Asset.Id,
            SecurityIncidentId = evt.Incident.Id,
            Title = $"Repair: {evt.Incident.Title}",
            Description = evt.Incident.Description,
            Priority = WorkOrderPriority.Critical,
            Status = WorkOrderStatus.New,
            AssetNameSnapshot = evt.Asset.Name,
            LocationNameSnapshot = evt.Location.Name,
            CorrelationId = evt.CorrelationId,
            SourceEventId = evt.EventId,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.WorkOrders.Add(workOrder);

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Created WorkOrder {WorkOrderId} from event {EventId} for incident {IncidentId}",
                workOrder.Id, evt.EventId, evt.Incident.Id);

            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Concurrent duplicate — another consumer already created the WorkOrder
            logger.LogInformation(
                "Concurrent duplicate detected for event {EventId} / incident {IncidentId}",
                evt.EventId, evt.Incident.Id);
            return true; // Idempotent success
        }
    }

    private static bool IsValid(IncidentCreatedV1 evt)
    {
        if (evt.EventId == Guid.Empty) return false;
        if (evt.EventType != IncidentCreatedV1.EventTypeName) return false;
        if (string.IsNullOrWhiteSpace(evt.CorrelationId)) return false;
        if (evt.Incident is null) return false;
        if (evt.Incident.Id == Guid.Empty) return false;
        if (string.IsNullOrWhiteSpace(evt.Incident.Title)) return false;
        if (string.IsNullOrWhiteSpace(evt.Incident.Description)) return false;
        if (string.IsNullOrWhiteSpace(evt.Incident.Severity)) return false;
        if (evt.Asset is null) return false;
        if (evt.Asset.Id == Guid.Empty) return false;
        if (string.IsNullOrWhiteSpace(evt.Asset.Name)) return false;
        if (string.IsNullOrWhiteSpace(evt.Asset.AssetType)) return false;
        if (evt.Location is null) return false;
        if (evt.Location.Id == Guid.Empty) return false;
        if (string.IsNullOrWhiteSpace(evt.Location.Name)) return false;
        if (evt.Location.BuildingId == Guid.Empty) return false;
        if (string.IsNullOrWhiteSpace(evt.Location.BuildingName)) return false;
        return true;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
