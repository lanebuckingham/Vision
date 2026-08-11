using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.SecurityOperationsService.Application.Assets.Queries;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.SecurityOperationsService.Application.Incidents.Queries;

public sealed class GetIncidentByIdQueryHandler(SecurityOperationsDbContext db)
    : IRequestHandler<GetIncidentByIdQuery, IncidentDetailDto?>
{
    public async Task<IncidentDetailDto?> Handle(
        GetIncidentByIdQuery request, CancellationToken cancellationToken)
    {
        var incident = await db.SecurityIncidents
            .AsNoTracking()
            .Where(i => i.Id == request.Id)
            .Select(i => new IncidentDetailDto(
                i.Id,
                i.Title,
                i.Description,
                i.Severity.ToString(),
                i.Status.ToString(),
                i.ResolutionSummary,
                i.CreatedAt,
                i.UpdatedAt,
                i.ResolvedAt,
                i.WorkOrderId,
                i.SecurityAsset != null
                    ? new IncidentAssetDetailDto(
                        i.SecurityAsset.Id,
                        i.SecurityAsset.Name,
                        i.SecurityAsset.AssetTag,
                        i.SecurityAsset.AssetType.ToString(),
                        i.SecurityAsset.Status.ToString())
                    : null,
                new LocationDto(i.Location.Id, i.Location.Name, i.Location.Floor, i.Location.Department),
                new BuildingDto(i.Location.Building.Id, i.Location.Building.Name)))
            .FirstOrDefaultAsync(cancellationToken);

        return incident;
    }
}
