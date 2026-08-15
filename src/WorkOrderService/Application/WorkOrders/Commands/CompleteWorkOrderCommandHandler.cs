using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Application.WorkOrders.Queries;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Application.WorkOrders.Commands;

public sealed class CompleteWorkOrderCommandHandler(
    WorkOrderDbContext db,
    ILogger<CompleteWorkOrderCommandHandler> logger)
    : IRequestHandler<CompleteWorkOrderCommand, WorkOrderDetailDto>
{
    public async Task<WorkOrderDetailDto> Handle(
        CompleteWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await db.WorkOrders
            .Include(w => w.AssignedTechnician)
            .Include(w => w.Notes)
            .FirstOrDefaultAsync(w => w.Id == request.WorkOrderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Work order '{request.WorkOrderId}' not found.");

        // Check completion information before domain transition — 400 not 409
        if (string.IsNullOrWhiteSpace(request.CompletionSummary) && workOrder.Notes.Count == 0)
            throw new ArgumentException(
                "Completion requires either a completion summary or at least one technician note.");

        // Domain behavior validates lifecycle status
        workOrder.Complete(request.CompletionSummary);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Work order {WorkOrderId} completed", workOrder.Id);

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
            workOrder.AssignedTechnician != null
                ? new AssignedTechnicianDetailDto(
                    workOrder.AssignedTechnician.Id,
                    workOrder.AssignedTechnician.DisplayName,
                    workOrder.AssignedTechnician.Email,
                    workOrder.AssignedTechnician.Specialty,
                    workOrder.AssignedTechnician.IsActive)
                : null,
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
                    workOrder.AssignedTechnician != null && n.TechnicianId == workOrder.AssignedTechnician.Id
                        ? workOrder.AssignedTechnician.DisplayName
                        : "Unknown",
                    n.Content,
                    n.CreatedAt))
                .ToList());
    }
}
