using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Application.Common;
using Vision.WorkOrderService.Domain;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Application.WorkOrders.Queries;

public sealed class GetWorkOrdersQueryHandler(WorkOrderDbContext db)
    : IRequestHandler<GetWorkOrdersQuery, PagedList<WorkOrderListItemDto>>
{
    public async Task<PagedList<WorkOrderListItemDto>> Handle(
        GetWorkOrdersQuery request, CancellationToken cancellationToken)
    {
        var query = db.WorkOrders
            .AsNoTracking()
            .Include(w => w.AssignedTechnician)
            .AsQueryable();

        // Filters
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = Enum.Parse<WorkOrderStatus>(request.Status, ignoreCase: true);
            query = query.Where(w => w.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            var priority = Enum.Parse<WorkOrderPriority>(request.Priority, ignoreCase: true);
            query = query.Where(w => w.Priority == priority);
        }

        if (request.TechnicianId.HasValue)
            query = query.Where(w => w.AssignedTechnicianId == request.TechnicianId.Value);

        if (request.AssetId.HasValue)
            query = query.Where(w => w.SecurityAssetId == request.AssetId.Value);

        if (request.IncidentId.HasValue)
            query = query.Where(w => w.SecurityIncidentId == request.IncidentId.Value);

        // Search — ILIKE across title, description, asset name, location name, technician name
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search;
            query = query.Where(w =>
                EF.Functions.ILike(w.Title, $"%{search}%") ||
                EF.Functions.ILike(w.Description, $"%{search}%") ||
                (w.AssetNameSnapshot != null && EF.Functions.ILike(w.AssetNameSnapshot, $"%{search}%")) ||
                (w.LocationNameSnapshot != null && EF.Functions.ILike(w.LocationNameSnapshot, $"%{search}%")) ||
                (w.AssignedTechnician != null && EF.Functions.ILike(w.AssignedTechnician.DisplayName, $"%{search}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(w => new WorkOrderListItemDto(
                w.Id,
                w.Title,
                w.Priority.ToString(),
                w.Status.ToString(),
                w.SecurityAssetId,
                w.SecurityIncidentId,
                w.AssetNameSnapshot,
                w.LocationNameSnapshot,
                w.AssignedTechnician != null
                    ? new AssignedTechnicianSummaryDto(
                        w.AssignedTechnician.Id,
                        w.AssignedTechnician.DisplayName,
                        w.AssignedTechnician.Specialty)
                    : null,
                w.AssignedAt,
                w.StartedAt,
                w.CompletedAt,
                w.CreatedAt,
                w.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedList<WorkOrderListItemDto>(items, request.Page, request.PageSize, totalCount);
    }
}
