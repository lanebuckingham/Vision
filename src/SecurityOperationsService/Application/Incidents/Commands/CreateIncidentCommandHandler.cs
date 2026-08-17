using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.SecurityOperationsService.Application.Assets.Queries;
using Vision.SecurityOperationsService.Application.Common;
using Vision.SecurityOperationsService.Application.Incidents.Queries;
using Vision.SecurityOperationsService.Domain;
using Vision.SecurityOperationsService.Infrastructure.Messaging;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.SecurityOperationsService.Application.Incidents.Commands;

public sealed class CreateIncidentCommandHandler(
    SecurityOperationsDbContext db,
    CorrelationContext correlationContext,
    ILogger<CreateIncidentCommandHandler> logger)
    : IRequestHandler<CreateIncidentCommand, IncidentDetailDto>
{
    public async Task<IncidentDetailDto> Handle(
        CreateIncidentCommand request, CancellationToken cancellationToken)
    {
        // Validate location exists
        var location = await db.Locations
            .AsNoTracking()
            .Include(l => l.Building)
            .FirstOrDefaultAsync(l => l.Id == request.LocationId, cancellationToken)
            ?? throw new ArgumentException($"Location '{request.LocationId}' does not exist.");

        // Validate asset if provided
        IncidentAssetDetailDto? assetDto = null;
        SecurityAsset? asset = null;
        if (request.AssetId.HasValue)
        {
            asset = await db.SecurityAssets
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.AssetId.Value, cancellationToken)
                ?? throw new ArgumentException($"Asset '{request.AssetId.Value}' does not exist.");

            if (asset.LocationId != request.LocationId)
                throw new ArgumentException(
                    $"Asset '{asset.Name}' belongs to a different location. Expected location '{request.LocationId}'.");

            assetDto = new IncidentAssetDetailDto(
                asset.Id, asset.Name, asset.AssetTag,
                asset.AssetType.ToString(), asset.Status.ToString());
        }

        var severity = Enum.Parse<IncidentSeverity>(request.Severity, ignoreCase: true);
        var now = DateTimeOffset.UtcNow;

        var incident = new SecurityIncident
        {
            Id = Guid.NewGuid(),
            LocationId = request.LocationId,
            SecurityAssetId = request.AssetId,
            Title = request.Title,
            Description = request.Description,
            Severity = severity,
            Status = IncidentStatus.Open,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.SecurityIncidents.Add(incident);

        // Qualification: Critical + asset -> write outbox message atomically
        if (severity == IncidentSeverity.Critical && asset != null)
        {
            var eventId = Guid.NewGuid();
            var correlationId = correlationContext.CorrelationId;

            var integrationEvent = new IncidentCreatedV1
            {
                EventId = eventId,
                OccurredAt = now,
                CorrelationId = correlationId,
                Incident = new IncidentCreatedIncidentV1
                {
                    Id = incident.Id,
                    Title = incident.Title,
                    Description = incident.Description,
                    Severity = severity.ToString()
                },
                Asset = new IncidentCreatedAssetV1
                {
                    Id = asset.Id,
                    Name = asset.Name,
                    AssetTag = asset.AssetTag,
                    AssetType = asset.AssetType.ToString()
                },
                Location = new IncidentCreatedLocationV1
                {
                    Id = location.Id,
                    Name = location.Name,
                    BuildingId = location.Building.Id,
                    BuildingName = location.Building.Name
                }
            };

            // Capture the current W3C trace context, if any, so the distributed trace can
            // be resumed when the background OutboxPublisher later sends this event to SQS.
            // Absence of a current Activity (e.g. a test or maintenance path) is expected
            // and must not block outbox creation — TraceParent/TraceState simply stay null.
            var currentActivity = System.Diagnostics.Activity.Current;

            var outboxMessage = new OutboxMessage
            {
                Id = eventId,
                EventType = IncidentCreatedV1.EventTypeName,
                Payload = JsonSerializer.Serialize(integrationEvent),
                OccurredAt = now,
                CorrelationId = correlationId,
                TraceParent = currentActivity?.Id,
                TraceState = currentActivity?.TraceStateString
            };

            db.OutboxMessages.Add(outboxMessage);

            logger.LogInformation(
                "Queued integration event {EventId} for incident {IncidentId}",
                eventId, incident.Id);
        }

        // Single SaveChangesAsync commits both incident + outbox atomically
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created security incident {IncidentId} for asset {AssetId} at location {LocationId} with correlation {CorrelationId}",
            incident.Id, request.AssetId, request.LocationId, correlationContext.CorrelationId);

        // Return the created incident as a detail DTO
        return new IncidentDetailDto(
            incident.Id,
            incident.Title,
            incident.Description,
            incident.Severity.ToString(),
            incident.Status.ToString(),
            incident.ResolutionSummary,
            incident.CreatedAt,
            incident.UpdatedAt,
            incident.ResolvedAt,
            incident.WorkOrderId,
            assetDto,
            new LocationDto(location.Id, location.Name, location.Floor, location.Department),
            new BuildingDto(location.Building.Id, location.Building.Name));
    }
}
