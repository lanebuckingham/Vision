using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.SecurityOperationsService.Application.Assets.Queries;
using Vision.SecurityOperationsService.Application.Incidents.Queries;
using Vision.SecurityOperationsService.Domain;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.SecurityOperationsService.Application.Incidents.Commands;

public sealed class UpdateIncidentStatusCommandHandler(
    SecurityOperationsDbContext db,
    ILogger<UpdateIncidentStatusCommandHandler> logger)
    : IRequestHandler<UpdateIncidentStatusCommand, IncidentDetailDto>
{
    public async Task<IncidentDetailDto> Handle(
        UpdateIncidentStatusCommand request, CancellationToken cancellationToken)
    {
        var incident = await db.SecurityIncidents
            .Include(i => i.Location)
                .ThenInclude(l => l.Building)
            .Include(i => i.SecurityAsset)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Incident '{request.Id}' not found.");

        var requestedStatus = Enum.Parse<IncidentStatus>(request.Status, ignoreCase: true);
        var oldStatus = incident.Status;

        // Delegate to domain behavior
        switch (requestedStatus)
        {
            case IncidentStatus.Investigating:
                incident.StartInvestigation();
                break;
            case IncidentStatus.Resolved:
                incident.Resolve(request.ResolutionSummary!);
                break;
            case IncidentStatus.Open:
                // Same-state idempotency for Open
                if (incident.Status != IncidentStatus.Open)
                    throw new InvalidOperationException("Cannot move an incident back to Open.");
                break;
        }

        await db.SaveChangesAsync(cancellationToken);

        if (oldStatus != incident.Status)
        {
            logger.LogInformation(
                "Security incident {IncidentId} moved from {OldStatus} to {NewStatus}",
                incident.Id, oldStatus, incident.Status);
        }

        // Build response DTO
        var assetDto = incident.SecurityAsset is not null
            ? new IncidentAssetDetailDto(
                incident.SecurityAsset.Id,
                incident.SecurityAsset.Name,
                incident.SecurityAsset.AssetTag,
                incident.SecurityAsset.AssetType.ToString(),
                incident.SecurityAsset.Status.ToString())
            : null;

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
            new LocationDto(incident.Location.Id, incident.Location.Name, incident.Location.Floor, incident.Location.Department),
            new BuildingDto(incident.Location.Building.Id, incident.Location.Building.Name));
    }
}
