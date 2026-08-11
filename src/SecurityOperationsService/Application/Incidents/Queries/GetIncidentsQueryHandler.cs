using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.SecurityOperationsService.Application.Assets.Queries;
using Vision.SecurityOperationsService.Application.Common;
using Vision.SecurityOperationsService.Domain;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.SecurityOperationsService.Application.Incidents.Queries;

public sealed class GetIncidentsQueryHandler(SecurityOperationsDbContext db)
    : IRequestHandler<GetIncidentsQuery, PagedList<IncidentListItemDto>>
{
    public async Task<PagedList<IncidentListItemDto>> Handle(
        GetIncidentsQuery request, CancellationToken cancellationToken)
    {
        var query = db.SecurityIncidents
            .AsNoTracking()
            .AsQueryable();

        // Filters
        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<IncidentStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(i => i.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Severity)
            && Enum.TryParse<IncidentSeverity>(request.Severity, ignoreCase: true, out var severity))
        {
            query = query.Where(i => i.Severity == severity);
        }

        if (request.AssetId.HasValue)
        {
            query = query.Where(i => i.SecurityAssetId == request.AssetId.Value);
        }

        if (request.BuildingId.HasValue)
        {
            query = query.Where(i => i.Location.BuildingId == request.BuildingId.Value);
        }

        if (request.LocationId.HasValue)
        {
            query = query.Where(i => i.LocationId == request.LocationId.Value);
        }

        // Search
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(i =>
                EF.Functions.ILike(i.Title, $"%{search}%") ||
                EF.Functions.ILike(i.Description, $"%{search}%") ||
                (i.SecurityAsset != null && EF.Functions.ILike(i.SecurityAsset.Name, $"%{search}%")) ||
                EF.Functions.ILike(i.Location.Name, $"%{search}%"));
        }

        // Count
        var totalCount = await query.CountAsync(cancellationToken);

        // Sort + paginate + project
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(i => new IncidentListItemDto(
                i.Id,
                i.Title,
                i.Severity.ToString(),
                i.Status.ToString(),
                i.SecurityAsset != null
                    ? new IncidentAssetDto(i.SecurityAsset.Id, i.SecurityAsset.Name, i.SecurityAsset.AssetType.ToString())
                    : null,
                new LocationDto(i.Location.Id, i.Location.Name, i.Location.Floor, i.Location.Department),
                i.CreatedAt,
                i.ResolvedAt,
                i.WorkOrderId))
            .ToListAsync(cancellationToken);

        return new PagedList<IncidentListItemDto>(items, request.Page, request.PageSize, totalCount);
    }
}
