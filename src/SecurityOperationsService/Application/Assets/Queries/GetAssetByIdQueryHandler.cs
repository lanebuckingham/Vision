using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.SecurityOperationsService.Application.Assets.Queries;

public sealed class GetAssetByIdQueryHandler(SecurityOperationsDbContext db)
    : IRequestHandler<GetAssetByIdQuery, AssetDetailDto?>
{
    private const int RecentIncidentLimit = 10;

    public async Task<AssetDetailDto?> Handle(
        GetAssetByIdQuery request, CancellationToken cancellationToken)
    {
        var asset = await db.SecurityAssets
            .AsNoTracking()
            .Where(a => a.Id == request.Id)
            .Select(a => new AssetDetailDto(
                a.Id,
                a.Name,
                a.AssetTag,
                a.AssetType.ToString(),
                a.Status.ToString(),
                a.Manufacturer,
                a.Model,
                a.Description,
                new BuildingDto(a.Location.Building.Id, a.Location.Building.Name),
                new LocationDto(a.Location.Id, a.Location.Name, a.Location.Floor, a.Location.Department),
                a.LastServiceAt,
                a.StatusChangedAt,
                a.Incidents
                    .OrderByDescending(i => i.CreatedAt)
                    .Take(RecentIncidentLimit)
                    .Select(i => new AssetIncidentDto(
                        i.Id,
                        i.Title,
                        i.Severity.ToString(),
                        i.Status.ToString(),
                        i.CreatedAt,
                        i.WorkOrderId))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return asset;
    }
}
