using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Application.WorkOrders.Queries;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Application.WorkOrders.Commands;

public sealed class AddTechnicianNoteCommandHandler(
    WorkOrderDbContext db,
    ILogger<AddTechnicianNoteCommandHandler> logger)
    : IRequestHandler<AddTechnicianNoteCommand, TechnicianNoteDto>
{
    public async Task<TechnicianNoteDto> Handle(
        AddTechnicianNoteCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await db.WorkOrders
            .Include(w => w.AssignedTechnician)
            .Include(w => w.Notes)
            .FirstOrDefaultAsync(w => w.Id == request.WorkOrderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Work order '{request.WorkOrderId}' not found.");

        if (workOrder.AssignedTechnicianId is null)
            throw new InvalidOperationException("Work order does not have an assigned technician.");

        // Domain behavior validates status (rejects New/Completed)
        workOrder.AddNote(workOrder.AssignedTechnicianId.Value, request.Content);

        await db.SaveChangesAsync(cancellationToken);

        var note = workOrder.Notes.OrderByDescending(n => n.CreatedAt).First();
        var technicianName = workOrder.AssignedTechnician?.DisplayName ?? "Unknown";

        logger.LogInformation(
            "Technician {TechnicianId} added note to work order {WorkOrderId}",
            workOrder.AssignedTechnicianId, workOrder.Id);

        return new TechnicianNoteDto(
            note.Id,
            note.TechnicianId,
            technicianName,
            note.Content,
            note.CreatedAt);
    }
}
