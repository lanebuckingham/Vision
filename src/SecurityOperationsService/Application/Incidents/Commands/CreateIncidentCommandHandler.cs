using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.SecurityOperationsService.Application.Assets.Queries;
using Vision.SecurityOperationsService.Application.Incidents.Queries;
using Vision.SecurityOperationsService.Domain;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.SecurityOperationsService.Application.Incidents.Commands;

public sealed class CreateIncidentCommandHandler(
    SecurityOperationsDbContext db,
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
        if (request.SecurityAssetId.HasValue)
        {
            var asset = await db.SecurityAssets
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.SecurityAssetId.Value, cancellationToken)
                ?? throw new ArgumentException($"Asset '{request.SecurityAssetId.Value}' does not exist.");

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
            SecurityAssetId = request.SecurityAssetId,
            Title = request.Title,
            Description = request.Description,
            Severity = severity,
            Status = IncidentStatus.Open,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.SecurityIncidents.Add(incident);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Creating security incident {IncidentId} for asset {AssetId} at location {LocationId}",
            incident.Id, request.SecurityAssetId, request.LocationId);

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
