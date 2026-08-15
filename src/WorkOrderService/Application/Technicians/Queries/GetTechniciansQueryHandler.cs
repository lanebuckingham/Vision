using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Application.Common;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Application.Technicians.Queries;

public sealed class GetTechniciansQueryHandler(WorkOrderDbContext db)
    : IRequestHandler<GetTechniciansQuery, PagedList<TechnicianListItemDto>>
{
    public async Task<PagedList<TechnicianListItemDto>> Handle(
        GetTechniciansQuery request, CancellationToken cancellationToken)
    {
        var query = db.Technicians.AsNoTracking().AsQueryable();

        if (request.ActiveOnly)
            query = query.Where(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search;
            query = query.Where(t =>
                EF.Functions.ILike(t.DisplayName, $"%{search}%") ||
                EF.Functions.ILike(t.Email, $"%{search}%") ||
                (t.Specialty != null && EF.Functions.ILike(t.Specialty, $"%{search}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(t => t.DisplayName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TechnicianListItemDto(
                t.Id,
                t.DisplayName,
                t.Email,
                t.Specialty,
                t.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedList<TechnicianListItemDto>(items, request.Page, request.PageSize, totalCount);
    }
}
