using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Application.WorkOrders.Queries;

public sealed class GetWorkOrderByIdQueryHandler(WorkOrderDbContext db)
    : IRequestHandler<GetWorkOrderByIdQuery, WorkOrderDetailDto?>
{
    public async Task<WorkOrderDetailDto?> Handle(
        GetWorkOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var result = await db.WorkOrders
            .AsNoTracking()
            .Include(w => w.AssignedTechnician)
            .Include(w => w.Notes)
            .Where(w => w.Id == request.Id)
            .Select(w => new WorkOrderDetailDto(
                w.Id,
                w.SecurityAssetId,
                w.SecurityIncidentId,
                w.Title,
                w.Description,
                w.Priority.ToString(),
                w.Status.ToString(),
                w.AssetNameSnapshot,
                w.LocationNameSnapshot,
                w.AssignedTechnician != null
                    ? new AssignedTechnicianDetailDto(
                        w.AssignedTechnician.Id,
                        w.AssignedTechnician.DisplayName,
                        w.AssignedTechnician.Email,
                        w.AssignedTechnician.Specialty,
                        w.AssignedTechnician.IsActive)
                    : null,
                w.AssignedAt,
                w.StartedAt,
                w.CompletedAt,
                w.CompletionSummary,
                w.CreatedAt,
                w.UpdatedAt,
                w.Notes
                    .OrderBy(n => n.CreatedAt)
                    .Select(n => new TechnicianNoteDto(
                        n.Id,
                        n.TechnicianId,
                        db.Technicians
                            .Where(t => t.Id == n.TechnicianId)
                            .Select(t => t.DisplayName)
                            .FirstOrDefault() ?? "Unknown",
                        n.Content,
                        n.CreatedAt))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return result;
    }
}
