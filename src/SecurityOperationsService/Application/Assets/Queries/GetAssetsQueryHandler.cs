using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.SecurityOperationsService.Application.Common;
using Vision.SecurityOperationsService.Domain;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.SecurityOperationsService.Application.Assets.Queries;

public sealed class GetAssetsQueryHandler(SecurityOperationsDbContext db)
    : IRequestHandler<GetAssetsQuery, PagedList<AssetListItemDto>>
{
    public async Task<PagedList<AssetListItemDto>> Handle(
        GetAssetsQuery request, CancellationToken cancellationToken)
    {
        var query = db.SecurityAssets
            .AsNoTracking()
            .Include(a => a.Location)
                .ThenInclude(l => l.Building)
            .AsQueryable();

        // Filters
        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<SecurityAssetStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Type)
            && Enum.TryParse<SecurityAssetType>(request.Type, ignoreCase: true, out var assetType))
        {
            query = query.Where(a => a.AssetType == assetType);
        }

        if (request.BuildingId.HasValue)
        {
            query = query.Where(a => a.Location.BuildingId == request.BuildingId.Value);
        }

        if (request.LocationId.HasValue)
        {
            query = query.Where(a => a.LocationId == request.LocationId.Value);
        }

        // Search
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(a =>
                EF.Functions.ILike(a.Name, $"%{search}%") ||
                (a.AssetTag != null && EF.Functions.ILike(a.AssetTag, $"%{search}%")) ||
                EF.Functions.ILike(a.Location.Name, $"%{search}%") ||
                EF.Functions.ILike(a.Location.Building.Name, $"%{search}%"));
        }

        // Count
        var totalCount = await query.CountAsync(cancellationToken);

        // Sort + paginate + project
        var items = await query
            .OrderBy(a => a.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AssetListItemDto(
                a.Id,
                a.Name,
                a.AssetTag,
                a.AssetType.ToString(),
                a.Status.ToString(),
                new BuildingDto(a.Location.Building.Id, a.Location.Building.Name),
                new LocationDto(a.Location.Id, a.Location.Name, a.Location.Floor, a.Location.Department),
                a.LastServiceAt,
                a.StatusChangedAt))
            .ToListAsync(cancellationToken);

        return new PagedList<AssetListItemDto>(items, request.Page, request.PageSize, totalCount);
    }
}
