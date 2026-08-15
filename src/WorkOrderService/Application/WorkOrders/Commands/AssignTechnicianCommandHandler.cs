using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Application.WorkOrders.Queries;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Application.WorkOrders.Commands;

public sealed class AssignTechnicianCommandHandler(
    WorkOrderDbContext db,
    ILogger<AssignTechnicianCommandHandler> logger)
    : IRequestHandler<AssignTechnicianCommand, WorkOrderDetailDto>
{
    public async Task<WorkOrderDetailDto> Handle(
        AssignTechnicianCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await db.WorkOrders
            .Include(w => w.Notes)
            .FirstOrDefaultAsync(w => w.Id == request.WorkOrderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Work order '{request.WorkOrderId}' not found.");

        var technician = await db.Technicians
            .FirstOrDefaultAsync(t => t.Id == request.TechnicianId, cancellationToken)
            ?? throw new KeyNotFoundException($"Technician '{request.TechnicianId}' not found.");

        // Domain behavior handles status/active validation
        workOrder.AssignTechnician(technician);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Assigned technician {TechnicianId} to work order {WorkOrderId}",
            technician.Id, workOrder.Id);

        return new WorkOrderDetailDto(
            workOrder.Id,
            workOrder.SecurityAssetId,
            workOrder.SecurityIncidentId,
            workOrder.Title,
            workOrder.Description,
            workOrder.Priority.ToString(),
            workOrder.Status.ToString(),
            workOrder.AssetNameSnapshot,
            workOrder.LocationNameSnapshot,
            new AssignedTechnicianDetailDto(
                technician.Id,
                technician.DisplayName,
                technician.Email,
                technician.Specialty,
                technician.IsActive),
            workOrder.AssignedAt,
            workOrder.StartedAt,
            workOrder.CompletedAt,
            workOrder.CompletionSummary,
            workOrder.CreatedAt,
            workOrder.UpdatedAt,
            workOrder.Notes
                .OrderBy(n => n.CreatedAt)
                .Select(n => new TechnicianNoteDto(
                    n.Id,
                    n.TechnicianId,
                    n.TechnicianId == technician.Id ? technician.DisplayName : "Unknown",
                    n.Content,
                    n.CreatedAt))
                .ToList());
    }
}
