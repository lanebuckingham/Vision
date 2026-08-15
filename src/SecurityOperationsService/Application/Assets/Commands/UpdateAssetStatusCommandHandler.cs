using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.SecurityOperationsService.Application.Assets.Queries;
using Vision.SecurityOperationsService.Domain;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.SecurityOperationsService.Application.Assets.Commands;

public sealed class UpdateAssetStatusCommandHandler(
    SecurityOperationsDbContext db,
    ILogger<UpdateAssetStatusCommandHandler> logger)
    : IRequestHandler<UpdateAssetStatusCommand, AssetDetailDto>
{
    public async Task<AssetDetailDto> Handle(
        UpdateAssetStatusCommand request, CancellationToken cancellationToken)
    {
        var asset = await db.SecurityAssets
            .Include(a => a.Location)
                .ThenInclude(l => l.Building)
            .Include(a => a.Incidents.Where(i => i.Status != IncidentStatus.Resolved)
                .OrderByDescending(i => i.CreatedAt)
                .Take(5))
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Asset '{request.Id}' not found.");

        var newStatus = Enum.Parse<SecurityAssetStatus>(request.Status, ignoreCase: true);

        asset.ChangeStatus(newStatus);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Asset {AssetId} status changed to {Status}",
            asset.Id, newStatus);

        return new AssetDetailDto(
            asset.Id,
            asset.Name,
            asset.AssetTag,
            asset.AssetType.ToString(),
            asset.Status.ToString(),
            asset.Manufacturer,
            asset.Model,
            asset.Description,
            new BuildingDto(asset.Location.Building.Id, asset.Location.Building.Name),
            new LocationDto(asset.Location.Id, asset.Location.Name, asset.Location.Floor, asset.Location.Department),
            asset.LastServiceAt,
            asset.StatusChangedAt,
            asset.Incidents
                .Select(i => new AssetIncidentDto(
                    i.Id, i.Title, i.Severity.ToString(), i.Status.ToString(), i.CreatedAt, i.WorkOrderId))
                .ToList());
    }
}
